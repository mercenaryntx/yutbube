using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Yutbube.Repositories
{
    public static class CosmosDbRepository
    {
        private static readonly string DatabaseId = Environment.GetEnvironmentVariable("CosmosDbDatabase");
        private static readonly string CollectionId = Environment.GetEnvironmentVariable("CosmosDbCollection");

        private static DocumentClient _client;
        private static DocumentClient Client
        {
            get
            {
                if (_client == null)
                {
                    _client = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("CosmosDbEndpoint")),
                        Environment.GetEnvironmentVariable("CosmosDbAuthKey"),
                        new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()});
                    CreateDatabaseIfNotExistsAsync().Wait();
                    CreateCollectionIfNotExistsAsync().Wait();
                }
                return _client;
            }
        }

        public static async Task<T> GetItemAsync<T>(string id) where T : class
        {
            try
            {
                return await Client.ReadDocumentAsync<T>(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                throw;
            }
        }

        public static async Task<IEnumerable<T>> GetItemsAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            var query = Client.CreateDocumentQuery<T>(
                    UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId),
                    new FeedOptions {MaxItemCount = -1, EnableCrossPartitionQuery = true})
                .Where(predicate)
                .AsDocumentQuery();

            var results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public static async Task<Document> CreateItemAsync<T>(T item) where T : class
        {
            return await Client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId), item);
        }

        public static async Task<Document> UpdateItemAsync<T>(string id, T item) where T : class
        {
            return await Client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id), item);
        }

        public static async Task DeleteItemAsync(string id)
        {
            await Client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id));
        }

        private static async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await Client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(DatabaseId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await Client.CreateDatabaseAsync(new Database { Id = DatabaseId });
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task CreateCollectionIfNotExistsAsync()
        {
            try
            {
                await Client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await _client.CreateDocumentCollectionAsync(
                        UriFactory.CreateDatabaseUri(DatabaseId),
                        new DocumentCollection
                        {
                            Id = CollectionId
                        },
                        new RequestOptions { OfferThroughput = 400 });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}