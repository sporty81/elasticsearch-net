﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using Elasticsearch.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace Nest.Resolvers.Writers
{
	public class TypeMappingWriter
	{
		private readonly Type _type;
		private readonly IConnectionSettingsValues _connectionSettings;
		private readonly NestSerializer _elasticSerializer;
		private ElasticInferrer Infer { get; set; }

		private int MaxRecursion { get; set; }
		private TypeNameMarker TypeName { get; set; }
		private ConcurrentDictionary<Type, int> SeenTypes { get; set; }

		public TypeMappingWriter(Type t, TypeNameMarker typeName, IConnectionSettingsValues connectionSettings, int maxRecursion)
		{
			this._type = t;
			this._connectionSettings = connectionSettings;

			this.TypeName = typeName;
			this.MaxRecursion = maxRecursion;

			this.SeenTypes = new ConcurrentDictionary<Type, int>();
			this.SeenTypes.TryAdd(t, 0);

			this._elasticSerializer = new NestSerializer(this._connectionSettings);
			this.Infer = new ElasticInferrer(this._connectionSettings);
		}

		/// <summary>
		/// internal constructor by TypeMappingWriter itself when it recurses, passes seenTypes as safeguard agains maxRecursion
		/// </summary>
		internal TypeMappingWriter(Type t, string typeName, IConnectionSettingsValues connectionSettings, int maxRecursion, ConcurrentDictionary<Type, int> seenTypes)
		{
			this._type = GetUnderlyingType(t);
			this._connectionSettings = connectionSettings;

			this.TypeName = typeName;
			this.MaxRecursion = maxRecursion;
			this.SeenTypes = seenTypes;

			this._elasticSerializer = new NestSerializer(this._connectionSettings);
			this.Infer = new ElasticInferrer(this._connectionSettings);
		}

		internal JObject MapPropertiesFromAttributes()
		{
			int seen;
			if (this.SeenTypes.TryGetValue(this._type, out seen) && seen > MaxRecursion)
				return JObject.Parse("{}");


			var sb = new StringBuilder();
			using (StringWriter sw = new StringWriter(sb))
			using (JsonWriter jsonWriter = new JsonTextWriter(sw))
			{

				jsonWriter.WriteStartObject();
				{
					this.WriteProperties(jsonWriter);
				}
				jsonWriter.WriteEnd();
				return JObject.Parse(sw.ToString());
			}
		}

		internal RootObjectMapping RootObjectMappingFromAttributes()
		{
			var json = JObject.Parse(this.MapFromAttributes());

			var nestedJson = json.Properties().First().Value.ToString();
			using (var ms = new MemoryStream(nestedJson.Utf8Bytes()))
				return this._elasticSerializer.DeserializeInternal<RootObjectMapping>(ms);
		}
		internal ObjectMapping ObjectMappingFromAttributes()
		{
			var json = JObject.Parse(this.MapFromAttributes());

			var nestedJson = json.Properties().First().Value.ToString();
			using (var ms = new MemoryStream(nestedJson.Utf8Bytes()))
				return this._elasticSerializer.DeserializeInternal<ObjectMapping>(ms);
		}
		internal NestedObjectMapping NestedObjectMappingFromAttributes()
		{
			var json = JObject.Parse(this.MapFromAttributes());

			var nestedJson = json.Properties().First().Value.ToString();
			using (var ms = new MemoryStream(nestedJson.Utf8Bytes()))
				return this._elasticSerializer.DeserializeInternal<NestedObjectMapping>(ms);
		}
		public string MapFromAttributes()
		{
			var sb = new StringBuilder();
			using (var sw = new StringWriter(sb))
			using (var jsonWriter = new JsonTextWriter(sw))
			{
				jsonWriter.Formatting = Formatting.Indented;
				jsonWriter.WriteStartObject();
				{
					var typeName = this.Infer.TypeName(this.TypeName);
					jsonWriter.WritePropertyName(typeName);
					jsonWriter.WriteStartObject();
					{
						jsonWriter.WritePropertyName("properties");
						jsonWriter.WriteStartObject();
						{
							this.WriteProperties(jsonWriter);
						}
						jsonWriter.WriteEnd();
					}
					jsonWriter.WriteEnd();
				}
				jsonWriter.WriteEndObject();

				return sw.ToString();
			}
		}

		internal void WriteProperties(JsonWriter jsonWriter)
		{
			var properties = this._type.GetProperties();
			foreach (var p in properties)
			{
				var att = ElasticAttributes.Property(p);
				if (att != null && att.OptOut)
					continue;

				var propertyName = this.Infer.PropertyName(p);
				var type = GetElasticSearchType(att, p);
				
				if (type == null) //could not get type from attribute or infer from CLR type.
					continue;

				jsonWriter.WritePropertyName(propertyName);
				jsonWriter.WriteStartObject();
				{
					if (att == null) //properties that follow can not be inferred from the CLR.
					{
						jsonWriter.WritePropertyName("type");
						jsonWriter.WriteValue(type);
						//jsonWriter.WriteEnd();
					}
					if (att != null)
						this.WritePropertiesFromAttribute(jsonWriter, att, propertyName, type);
					if (type == "object" || type == "nested")
					{

						var deepType = p.PropertyType;
						var deepTypeName = this.Infer.TypeName(deepType);
						var seenTypes = new ConcurrentDictionary<Type, int>(this.SeenTypes);
						seenTypes.AddOrUpdate(deepType, 0, (t, i) => ++i);

						var newTypeMappingWriter = new TypeMappingWriter(deepType, deepTypeName, this._connectionSettings, MaxRecursion, seenTypes);
						var nestedProperties = newTypeMappingWriter.MapPropertiesFromAttributes();

						jsonWriter.WritePropertyName("properties");
						nestedProperties.WriteTo(jsonWriter);
					}
				}
				jsonWriter.WriteEnd();
			}
		}

		private void WritePropertiesFromAttribute(JsonWriter jsonWriter, IElasticPropertyAttribute att, string propertyName, string type)
		{
			var visitor = new WritePropertiesFromAttributeVisitor(jsonWriter, propertyName, type);
			att.Accept(visitor);

		}

		/// <summary>
		/// Get the Elastic Search Field Type Related.
		/// </summary>
		/// <param name="att">ElasticPropertyAttribute</param>
		/// <param name="p">Property Field</param>
		/// <returns>String with the type name or null if can not be inferres</returns>
		private string GetElasticSearchType(IElasticPropertyAttribute att, PropertyInfo p)
		{
			FieldType? fieldType = null;
			if (att != null)
			{
				fieldType = att.Type;
			}

			if (fieldType == null || fieldType == FieldType.none)
			{
				fieldType = this.GetFieldTypeFromType(p.PropertyType);
			}

			return this.GetElasticSearchTypeFromFieldType(fieldType);
		}

		/// <summary>
		/// Get the Elastic Search Field from a FieldType.
		/// </summary>
		/// <param name="fieldType">FieldType</param>
		/// <returns>String with the type name or null if can not be inferres</returns>
		private string GetElasticSearchTypeFromFieldType(FieldType? fieldType)
		{
			switch (fieldType)
			{
				case FieldType.geo_point:
					return "geo_point";
				case FieldType.attachment:
					return "attachment";
				case FieldType.ip:
					return "ip";
				case FieldType.binary:
					return "binary";
				case FieldType.string_type:
					return "string";
				case FieldType.integer_type:
					return "integer";
				case FieldType.long_type:
					return "long";
				case FieldType.float_type:
					return "float";
				case FieldType.double_type:
					return "double";
				case FieldType.date_type:
					return "date";
				case FieldType.boolean_type:
					return "boolean";
				case FieldType.completion:
					return "completion";
        case FieldType.nested:
          return "nested";
				case FieldType.@object:
					return "object";
				default:
					return null;
			}
		}

		/// <summary>
		/// Inferes the FieldType from the type of the property.
		/// </summary>
		/// <param name="propertyType">Type of the property</param>
		/// <returns>FieldType or null if can not be inferred</returns>
		private FieldType? GetFieldTypeFromType(Type propertyType)
		{
			propertyType = GetUnderlyingType(propertyType);

			if (propertyType == typeof(string))
				return FieldType.string_type;

			if (propertyType.IsValueType)
			{
				switch (propertyType.Name)
				{
					case "Int32":
						return FieldType.integer_type;
					case "Int64":
						return FieldType.long_type;
					case "Single":
						return FieldType.float_type;
					case "Decimal":
					case "Double":
						return FieldType.double_type;
					case "DateTime":
						return FieldType.date_type;
					case "Boolean":
						return FieldType.boolean_type;
				}
			}
			else
				return FieldType.@object;
			return null;
		}

		private static Type GetUnderlyingType(Type type)
		{
			if (type.IsArray)
				return type.GetElementType();

			if (type.IsGenericType && type.GetGenericArguments().Length >= 1)
				return type.GetGenericArguments()[0];

			return type;
		}
	}
}
