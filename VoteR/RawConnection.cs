using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.HttpFeature;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace VoteR
{
    public class RawConnection : PersistentConnection
    {
        private static readonly ConcurrentDictionary<string, string> _users = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _clients = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _votes = new ConcurrentDictionary<string, string>();

        protected override async Task OnConnected(HttpRequest request, string connectionId)
        {
            var userName = request.Cookies["user"];
            if (!string.IsNullOrEmpty(userName))
            {
                _clients[connectionId] = userName;
                _users[userName] = connectionId;
            }

            string clientIp = GetClientIP(request);

            string user = GetUser(connectionId);

            await Groups.Add(connectionId, "foo");
            await Connection.Broadcast(DateTime.Now + ": " + user + " joined from " + clientIp);
        }

        protected override Task OnReconnected(HttpRequest request, string connectionId)
        {
            string user = GetUser(connectionId);

            return Connection.Broadcast(DateTime.Now + ": " + user + " reconnected");
        }

        protected override Task OnDisconnected(HttpRequest request, string connectionId, bool stopCalled)
        {
            string ignored;
            _users.TryRemove(GetUser(connectionId), out ignored);

            string suffix = stopCalled ? "cleanly" : "uncleanly";
            return Connection.Broadcast(DateTime.Now + ": " + GetUser(connectionId) + " disconnected " + suffix);
        }

        protected override Task OnReceived(HttpRequest request, string connectionId, string data)
        {
            var message = JsonConvert.DeserializeObject<Message>(data);

            switch (message.Type)
            {
                case MessageType.Join:
                    string name = message.Value;
                    _clients[connectionId] = name;
                    _users[name] = connectionId;
                    Connection.Broadcast(new
                    {
                        type = MessageType.Join.ToString(),
                        name = message.Value
                    });
                    break;
                case MessageType.Vote:
                    _votes[GetUser(connectionId)] = message.Value;
                    Connection.Broadcast(new
                    {
                        type = MessageType.Vote.ToString(),
                        from = GetUser(connectionId),
                        vote = message.Value
                    });
                    if (_votes.Count == _users.Count)
                    {
                        Connection.Broadcast(new
                        {
                            type = MessageType.VoteResult.ToString(),
                            result = JsonConvert.SerializeObject(_votes)
                        });
                        _votes.Clear();
                    }
                    break;
                default:
                    break;
            }

            return base.OnReceived(request, connectionId, data);
        }

        private string GetUser(string connectionId)
        {
            string user;
            if (!_clients.TryGetValue(connectionId, out user))
            {
                return connectionId;
            }
            return user;
        }

        private string GetClient(string user)
        {
            string connectionId;
            if (_users.TryGetValue(user, out connectionId))
            {
                return connectionId;
            }
            return null;
        }

        enum MessageType
        {
            Join,
            Vote,
            VoteResult
        }

        class Message
        {
            public MessageType Type { get; set; }
            public string Value { get; set; }
        }

        private static string GetClientIP(HttpRequest request)
        {
            var conn = request.HttpContext.GetFeature<IHttpConnectionFeature>();
            return conn != null ? conn.RemoteIpAddress.ToString() : null;
        }
    }
}