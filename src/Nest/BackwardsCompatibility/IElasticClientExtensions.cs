﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nest
{
	public static class IELasticClientExtensions
	{
		/// <summary>
		/// Untyped count, defaults to all indices and types
		/// </summary>
		/// <param name="countSelector">filter on indices and types or pass pararameters</param>
		public static ICountResponse Count(this IElasticClient client,
			Func<CountDescriptor<dynamic>, CountDescriptor<dynamic>> countSelector = null)
		{
			countSelector = countSelector ?? (s => s);
			return client.Count<dynamic>(c => countSelector(c.AllIndices().AllTypes()));
		}
		/// <summary>
		/// Untyped count, defaults to all indices and types
		/// </summary>
		/// <param name="countSelector">filter on indices and types or pass pararameters</param>
		public static Task<ICountResponse> CountAsync(this IElasticClient client,
			Func<CountDescriptor<dynamic>, CountDescriptor<dynamic>> countSelector = null)
		{
			countSelector = countSelector ?? (s => s);
			return client.CountAsync<dynamic>(c => countSelector(c.AllIndices().AllTypes()));
		}

		public static IIndicesOperationResponse OpenIndex(this IElasticClient client, string index)
		{
			return client.OpenIndex(f => f.Index(index));
		}

		public static IIndicesOperationResponse OpenIndex<T>(this IElasticClient client)
			where T : class
		{
			return client.OpenIndex(f => f.Index<T>());
		}

		public static IIndicesOperationResponse CloseIndex(this IElasticClient client, string index)
		{
			return client.CloseIndex(f => f.Index(index));
		}

		public static IIndicesOperationResponse CloseIndex<T>(this IElasticClient client)
			where T : class
		{
			return client.CloseIndex(f => f.Index<T>());
		}



		public static T Source<T>(this IElasticClient client, string id, string index = null, string type = null)
			where T : class
		{
			return client.Source<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static T Source<T>(this IElasticClient client, int id, string index = null, string type = null)
			where T : class
		{
			return client.Source<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static Task<T> SourceAsync<T>(this IElasticClient client, string id, string index = null, string type = null)
			where T : class
		{
			return client.SourceAsync<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static Task<T> SourceAsync<T>(this IElasticClient client, int id, string index = null, string type = null)
			where T : class
		{
			return client.SourceAsync<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static IGetResponse<T> Get<T>(this IElasticClient client, string id, string index = null, string type = null)
			where T : class
		{
			return client.Get<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static IGetResponse<T> Get<T>(this IElasticClient client, int id, string index = null, string type = null)
			where T : class
		{
			return client.Get<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static Task<IGetResponse<T>> GetAsync<T>(this IElasticClient client, string id, string index = null, string type = null)
			where T : class
		{
			return client.GetAsync<T>(s => s.Id(id).Index(index).Type(type));
		}
		public static Task<IGetResponse<T>> GetAsync<T>(this IElasticClient client, int id, string index = null, string type = null)
			where T : class
		{
			return client.GetAsync<T>(s => s.Id(id).Index(index).Type(type));
		}

		public static IEnumerable<T> SourceMany<T>(this IElasticClient client, IEnumerable<string> ids, string index = null, string type = null)
			where T : class
		{
			var result = client.MultiGet(s => s.GetMany<T>(ids, (gs, i) => gs.Index(index).Type(type)));
			return result.SourceMany<T>(ids);
		}
		public static IEnumerable<T> SourceMany<T>(this IElasticClient client, IEnumerable<int> ids, string index = null, string type = null)
			where T : class
		{
			return client.SourceMany<T>(ids.Select(i => i.ToString(CultureInfo.InvariantCulture)), index, type);
		}
		public static Task<IEnumerable<T>> SourceManyAsync<T>(this IElasticClient client, IEnumerable<string> ids, string index = null, string type = null)
			where T : class
		{
			return client.MultiGetAsync(s => s.GetMany<T>(ids, (gs, i) => gs.Index(index).Type(type)))
				.ContinueWith(t => t.Result.SourceMany<T>(ids));
		}
		public static Task<IEnumerable<T>> SourceManyAsync<T>(this IElasticClient client, IEnumerable<int> ids, string index = null, string type = null)
			where T : class
		{
			return client.SourceManyAsync<T>(ids.Select(i=>i.ToString(CultureInfo.InvariantCulture)), index, type);
		}
		public static IEnumerable<IMultiGetHit<T>> GetMany<T>(this IElasticClient client, IEnumerable<string> ids, string index = null, string type = null)
			where T : class
		{
			var result = client.MultiGet(s => s.GetMany<T>(ids, (gs, i) => gs.Index(index).Type(type)));
			return result.GetMany<T>(ids);
		}
		public static IEnumerable<IMultiGetHit<T>> GetMany<T>(this IElasticClient client, IEnumerable<int> ids, string index = null, string type = null)
			where T : class
		{
			return client.GetMany<T>(ids.Select(i => i.ToString(CultureInfo.InvariantCulture)), index, type);
		}
		public static Task<IEnumerable<IMultiGetHit<T>>> GetManyAsync<T>(this IElasticClient client, IEnumerable<string> ids, string index = null, string type = null)
			where T : class
		{
			return client.MultiGetAsync(s => s.GetMany<T>(ids, (gs, i) => gs.Index(index).Type(type)))
				.ContinueWith(t => t.Result.GetMany<T>(ids));
		}
		public static Task<IEnumerable<IMultiGetHit<T>>> GetManyAsync<T>(this IElasticClient client, IEnumerable<int> ids, string index = null, string type = null)
			where T : class
		{
			return client.GetManyAsync<T>(ids.Select(i=>i.ToString(CultureInfo.InvariantCulture)), index, type);
		}
	}
}