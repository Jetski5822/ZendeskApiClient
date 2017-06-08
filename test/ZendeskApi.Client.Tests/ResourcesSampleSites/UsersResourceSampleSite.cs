﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using ZendeskApi.Client.Tests.ResourcesSampleSites;
using ZendeskApi.Client.Models;
using ZendeskApi.Client.Requests;
using ZendeskApi.Client.Responses;

namespace ZendeskApi.Client.Tests
{
    public class UsersResourceSampleSite : SampleSite
    {
        private class State
        {
            public IDictionary<long, User> Users = new Dictionary<long, User>();
        }

        public static Random RAND = new Random();

        public static Action<IRouteBuilder> MatchesRequest
        {
            get
            {
                return rb => rb
                    .MapGet("api/v2/users/show_many", (req, resp, routeData) =>
                    {
                        var ids = req.Query["ids"].ToString().Split(',').Select(long.Parse);

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var users = state.Users.Where(x => ids.Contains(x.Key)).Select(p => p.Value);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UsersResponse { Item = users }));
                    })
                    .MapGet("api/v2/users/{id}", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var user = state.Users.Single(x => x.Key == id).Value;

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UserResponse { Item = user }));
                    })
                    .MapGet("api/v2/users", (req, resp, routeData) =>
                    {
                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UsersResponse { Item = state.Users.Values }));
                    })
                    .MapGet("api/v2/groups/{id}/users", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var users = state
                            .Users
                            .Where(x => x.Value.DefaultGroupId == id)
                            .Select(p => p.Value);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UsersResponse { Item = users }));
                    })
                    .MapGet("api/v2/organizations/{id}/users", (req, resp, routeData) =>
                    {
                        var id = long.Parse(routeData.Values["id"].ToString());

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        var users = state
                            .Users
                            .Where(x => x.Value.OrganizationId.HasValue)
                            .Where(x => x.Value.OrganizationId == id)
                            .Select(p => p.Value);

                        resp.StatusCode = (int)HttpStatusCode.OK;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UsersResponse { Item = users }));
                    })
                    .MapPost("api/v2/users", (req, resp, routeData) =>
                    {
                        var user = req.Body.Deserialize<UserRequest>().Item;

                        if (user.Tags != null && user.Tags.Contains("error"))
                        {
                            resp.StatusCode = (int)HttpStatusCode.PaymentRequired; // It doesnt matter as long as not 201

                            return Task.CompletedTask;
                        }

                        var state = req.HttpContext.RequestServices.GetRequiredService<State>();

                        user.Id = long.Parse(RAND.Next().ToString());
                        state.Users.Add(user.Id.Value, user);

                        resp.StatusCode = (int)HttpStatusCode.Created;
                        return resp.WriteAsync(JsonConvert.SerializeObject(new UserResponse { Item = user }));
                    })
                    ;
            }
        }

        private readonly TestServer _server;

        private HttpClient _client;
        public override HttpClient Client => _client;

        public UsersResourceSampleSite(string resource)
        {
            var webhostbuilder = new WebHostBuilder();
            webhostbuilder
                .ConfigureServices(services => {
                    services.AddSingleton<State>((_) => new State());
                    services.AddRouting();
                    services.AddMemoryCache();
                })
                .Configure(app =>
                {
                    app.UseRouter(MatchesRequest);
                });

            _server = new TestServer(webhostbuilder);
            _client = _server.CreateClient();

            RefreshClient(resource);
        }

        public override void RefreshClient(string resource)
        {
            _client = _server.CreateClient();
            _client.BaseAddress = new Uri($"http://localhost/{CreateResource(resource)}");
        }

        private string CreateResource(string resource)
        {
            resource = resource?.Trim('/');

            return resource != null ? resource + "/" : resource;
        }

        public Uri BaseUri
        {
            get { return Client.BaseAddress; }
        }

        public override void Dispose()
        {
            Client.Dispose();
            _server.Dispose();
        }
    }
}