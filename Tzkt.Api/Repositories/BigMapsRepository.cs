﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using Netezos.Encoding;
using Netezos.Contracts;
using Tzkt.Api.Models;
using Tzkt.Api.Services.Cache;

namespace Tzkt.Api.Repositories
{
    public class BigMapsRepository : DbConnection
    {
        readonly AccountsCache Accounts;
        readonly TimeCache Times;

        public BigMapsRepository(AccountsCache accounts, TimeCache times, IConfiguration config) : base(config)
        {
            Accounts = accounts;
            Times = times;
        }

        #region bigmaps
        public async Task<int> GetCount()
        {
            using var db = GetConnection();
            return await db.QueryFirstAsync<int>(@"SELECT COUNT(*) FROM ""BigMaps""");
        }

        public async Task<MichelinePrim> GetMicheType(int ptr)
        {
            var sql = @"
                SELECT  ""KeyType"", ""ValueType""
                FROM    ""BigMaps""
                WHERE   ""Ptr"" = @ptr
                LIMIT   1";

            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { ptr });
            if (row == null) return null;

            return new MichelinePrim
            {
                Prim = PrimType.big_map,
                Args = new List<IMicheline>
                {
                    Micheline.FromBytes(row.KeyType),
                    Micheline.FromBytes(row.ValueType),
                }
            };
        }

        public async Task<BigMap> Get(int ptr, MichelineFormat micheline)
        {
            var sql = @"
                SELECT  *
                FROM    ""BigMaps""
                WHERE   ""Ptr"" = @ptr
                LIMIT   1";

            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { ptr });
            if (row == null) return null;

            return ReadBigMap(row, micheline);
        }

        public async Task<IEnumerable<BigMap>> Get(
            AccountParameter contract,
            bool? active,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            MichelineFormat micheline)
        {
            var sql = new SqlBuilder(@"SELECT * FROM ""BigMaps""")
                .Filter("ContractId", contract)
                .Filter("Active", active)
                .Take(sort, offset, limit, x => x switch
                {
                    "ptr" => ("Ptr", "Ptr"),
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "totalKeys" => ("TotalKeys", "TotalKeys"),
                    "activeKeys" => ("ActiveKeys", "ActiveKeys"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            return rows.Select(row => (BigMap)ReadBigMap(row, micheline));
        }

        public async Task<object[][]> Get(
            AccountParameter contract,
            bool? active,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            string[] fields,
            MichelineFormat micheline)
        {
            var columns = new HashSet<string>(fields.Length);
            foreach (var field in fields)
            {
                switch (field)
                {
                    case "ptr": columns.Add(@"""Ptr"""); break;
                    case "contract": columns.Add(@"""ContractId"""); break;
                    case "path": columns.Add(@"""StoragePath"""); break;
                    case "active": columns.Add(@"""Active"""); break;
                    case "firstLevel": columns.Add(@"""FirstLevel"""); break;
                    case "lastLevel": columns.Add(@"""LastLevel"""); break;
                    case "totalKeys": columns.Add(@"""TotalKeys"""); break;
                    case "activeKeys": columns.Add(@"""ActiveKeys"""); break;
                    case "updates": columns.Add(@"""Updates"""); break;
                    case "keyType": columns.Add(@"""KeyType"""); break;
                    case "valueType": columns.Add(@"""ValueType"""); break;
                }
            }

            if (columns.Count == 0)
                return Array.Empty<object[]>();

            var sql = new SqlBuilder($@"SELECT {string.Join(',', columns)} FROM ""BigMaps""")
                .Filter("ContractId", contract)
                .Filter("Active", active)
                .Take(sort, offset, limit, x => x switch
                {
                    "ptr" => ("Ptr", "Ptr"),
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "totalKeys" => ("TotalKeys", "TotalKeys"),
                    "activeKeys" => ("ActiveKeys", "ActiveKeys"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            var result = new object[rows.Count()][];
            for (int i = 0; i < result.Length; i++)
                result[i] = new object[fields.Length];

            for (int i = 0, j = 0; i < fields.Length; j = 0, i++)
            {
                switch (fields[i])
                {
                    case "ptr":
                        foreach (var row in rows)
                            result[j++][i] = row.Ptr;
                        break;
                    case "contract":
                        foreach (var row in rows)
                            result[j++][i] = Accounts.GetAlias(row.ContractId);
                        break;
                    case "path":
                        foreach (var row in rows)
                            result[j++][i] = ((string)row.StoragePath).Replace(".", "..").Replace(',', '.');
                        break;
                    case "active":
                        foreach (var row in rows)
                            result[j++][i] = row.Active;
                        break;
                    case "firstLevel":
                        foreach (var row in rows)
                            result[j++][i] = row.FirstLevel;
                        break;
                    case "lastLevel":
                        foreach (var row in rows)
                            result[j++][i] = row.LastLevel;
                        break;
                    case "totalKeys":
                        foreach (var row in rows)
                            result[j++][i] = row.TotalKeys;
                        break;
                    case "activeKeys":
                        foreach (var row in rows)
                            result[j++][i] = row.ActiveKeys;
                        break;
                    case "updates":
                        foreach (var row in rows)
                            result[j++][i] = row.Updates;
                        break;
                    case "keyType":
                        foreach (var row in rows)
                            result[j++][i] = (int)micheline < 2
                                ? new RawJson(Schema.Create(Micheline.FromBytes(row.KeyType) as MichelinePrim).Humanize())
                                : new RawJson(Micheline.ToJson(row.KeyType));
                        break;
                    case "valueType":
                        foreach (var row in rows)
                            result[j++][i] = (int)micheline < 2
                                ? new RawJson(Schema.Create(Micheline.FromBytes(row.ValueType) as MichelinePrim).Humanize())
                                : new RawJson(Micheline.ToJson(row.ValueType));
                        break;
                }
            }

            return result;
        }

        public async Task<object[]> Get(
            AccountParameter contract,
            bool? active,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            string field,
            MichelineFormat micheline)
        {
            var columns = new HashSet<string>(1);
            switch (field)
            {
                case "ptr": columns.Add(@"""Ptr"""); break;
                case "contract": columns.Add(@"""ContractId"""); break;
                case "path": columns.Add(@"""StoragePath"""); break;
                case "active": columns.Add(@"""Active"""); break;
                case "firstLevel": columns.Add(@"""FirstLevel"""); break;
                case "lastLevel": columns.Add(@"""LastLevel"""); break;
                case "totalKeys": columns.Add(@"""TotalKeys"""); break;
                case "activeKeys": columns.Add(@"""ActiveKeys"""); break;
                case "updates": columns.Add(@"""Updates"""); break;
                case "keyType": columns.Add(@"""KeyType"""); break;
                case "valueType": columns.Add(@"""ValueType"""); break;
            }

            if (columns.Count == 0)
                return Array.Empty<object>();

            var sql = new SqlBuilder($@"SELECT {string.Join(',', columns)} FROM ""BigMaps""")
                .Filter("ContractId", contract)
                .Filter("Active", active)
                .Take(sort, offset, limit, x => x switch
                {
                    "ptr" => ("Ptr", "Ptr"),
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "totalKeys" => ("TotalKeys", "TotalKeys"),
                    "activeKeys" => ("ActiveKeys", "ActiveKeys"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            //TODO: optimize memory allocation
            var result = new object[rows.Count()];
            var j = 0;

            switch (field)
            {
                case "ptr":
                    foreach (var row in rows)
                        result[j++] = row.Ptr;
                    break;
                case "contract":
                    foreach (var row in rows)
                        result[j++] = Accounts.GetAlias(row.ContractId);
                    break;
                case "path":
                    foreach (var row in rows)
                        result[j++] = ((string)row.StoragePath).Replace(".", "..").Replace(',', '.');
                    break;
                case "active":
                    foreach (var row in rows)
                        result[j++] = row.Active;
                    break;
                case "firstLevel":
                    foreach (var row in rows)
                        result[j++] = row.FirstLevel;
                    break;
                case "lastLevel":
                    foreach (var row in rows)
                        result[j++] = row.LastLevel;
                    break;
                case "totalKeys":
                    foreach (var row in rows)
                        result[j++] = row.TotalKeys;
                    break;
                case "activeKeys":
                    foreach (var row in rows)
                        result[j++] = row.ActiveKeys;
                    break;
                case "updates":
                    foreach (var row in rows)
                        result[j++] = row.Updates;
                    break;
                case "keyType":
                    foreach (var row in rows)
                        result[j++] = (int)micheline < 2
                            ? new RawJson(Schema.Create(Micheline.FromBytes(row.KeyType) as MichelinePrim).Humanize())
                            : new RawJson(Micheline.ToJson(row.KeyType));
                    break;
                case "valueType":
                    foreach (var row in rows)
                        result[j++] = (int)micheline < 2
                            ? new RawJson(Schema.Create(Micheline.FromBytes(row.ValueType) as MichelinePrim).Humanize())
                            : new RawJson(Micheline.ToJson(row.ValueType));
                    break;
            }

            return result;
        }
        #endregion

        #region bigmap keys
        public async Task<BigMapKey> GetKey(
            int ptr,
            string key,
            MichelineFormat micheline)
        {
            var sql = @"
                SELECT  *
                FROM    ""BigMapKeys""
                WHERE   ""BigMapPtr"" = @ptr
                AND     ""JsonKey"" = @key::jsonb
                LIMIT   1";

            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { ptr, key });
            if (row == null) return null;

            return ReadBigMapKey(row, micheline);
        }

        public async Task<BigMapKey> GetKeyByHash(
            int ptr,
            string hash,
            MichelineFormat micheline)
        {
            var sql = @"
                SELECT  *
                FROM    ""BigMapKeys""
                WHERE   ""BigMapPtr"" = @ptr
                AND     ""KeyHash"" = @hash::character(54)
                LIMIT   1";

            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { ptr, hash });
            if (row == null) return null;

            return ReadBigMapKey(row, micheline);
        }

        public async Task<IEnumerable<BigMapKey>> GetKeys(
            int ptr,
            bool? active,
            JsonParameter key,
            JsonParameter value,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            MichelineFormat micheline)
        {
            var sql = new SqlBuilder(@"SELECT * FROM ""BigMapKeys""")
                .Filter("BigMapPtr", ptr)
                .Filter("Active", active)
                .Filter("JsonKey", key)
                .Filter("JsonValue", value)
                .Take(sort, offset, limit, x => x switch
                {
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            return rows.Select(row => (BigMapKey)ReadBigMapKey(row, micheline));
        }

        public async Task<object[][]> GetKeys(
            int ptr,
            bool? active,
            JsonParameter key,
            JsonParameter value,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            string[] fields,
            MichelineFormat micheline)
        {
            var columns = new HashSet<string>(fields.Length);
            foreach (var field in fields)
            {
                switch (field)
                {
                    case "active": columns.Add(@"""Active"""); break;
                    case "firstLevel": columns.Add(@"""FirstLevel"""); break;
                    case "lastLevel": columns.Add(@"""LastLevel"""); break;
                    case "updates": columns.Add(@"""Updates"""); break;
                    case "hash": columns.Add(@"""KeyHash"""); break;
                    case "key":
                        columns.Add((int)micheline < 2 ? @"""JsonKey""" : @"""RawKey""");
                        break;
                    case "value":
                        columns.Add((int)micheline < 2 ? @"""JsonValue""" : @"""RawValue""");
                        break;
                }
            }

            if (columns.Count == 0)
                return Array.Empty<object[]>();

            var sql = new SqlBuilder($@"SELECT {string.Join(',', columns)} FROM ""BigMapKeys""")
                .Filter("BigMapPtr", ptr)
                .Filter("Active", active)
                .Filter("JsonKey", key)
                .Filter("JsonValue", value)
                .Take(sort, offset, limit, x => x switch
                {
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            var result = new object[rows.Count()][];
            for (int i = 0; i < result.Length; i++)
                result[i] = new object[fields.Length];

            for (int i = 0, j = 0; i < fields.Length; j = 0, i++)
            {
                switch (fields[i])
                {
                    case "active":
                        foreach (var row in rows)
                            result[j++][i] = row.Active;
                        break;
                    case "firstLevel":
                        foreach (var row in rows)
                            result[j++][i] = row.FirstLevel;
                        break;
                    case "lastLevel":
                        foreach (var row in rows)
                            result[j++][i] = row.LastLevel;
                        break;
                    case "updates":
                        foreach (var row in rows)
                            result[j++][i] = row.Updates;
                        break;
                    case "hash":
                        foreach (var row in rows)
                            result[j++][i] = row.KeyHash;
                        break;
                    case "key":
                        foreach (var row in rows)
                            result[j++][i] = micheline switch
                            {
                                MichelineFormat.Json => new RawJson(row.JsonKey),
                                MichelineFormat.JsonString => row.JsonKey,
                                MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawKey)),
                                MichelineFormat.RawString => Micheline.ToJson(row.RawKey),
                                _ => null
                            };
                        break;
                    case "value":
                        foreach (var row in rows)
                            result[j++][i] = micheline switch
                            {
                                MichelineFormat.Json => new RawJson(row.JsonValue),
                                MichelineFormat.JsonString => row.JsonValue,
                                MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawValue)),
                                MichelineFormat.RawString => Micheline.ToJson(row.RawValue),
                                _ => null
                            };
                        break;
                }
            }

            return result;
        }

        public async Task<object[]> GetKeys(
            int ptr,
            bool? active,
            JsonParameter key,
            JsonParameter value,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            string field,
            MichelineFormat micheline)
        {
            var columns = new HashSet<string>(1);
            switch (field)
            {
                case "active": columns.Add(@"""Active"""); break;
                case "firstLevel": columns.Add(@"""FirstLevel"""); break;
                case "lastLevel": columns.Add(@"""LastLevel"""); break;
                case "updates": columns.Add(@"""Updates"""); break;
                case "hash": columns.Add(@"""KeyHash"""); break;
                case "key":
                    columns.Add((int)micheline < 2 ? @"""JsonKey""" : @"""RawKey""");
                    break;
                case "value":
                    columns.Add((int)micheline < 2 ? @"""JsonValue""" : @"""RawValue""");
                    break;
            }

            if (columns.Count == 0)
                return Array.Empty<object>();

            var sql = new SqlBuilder($@"SELECT {string.Join(',', columns)} FROM ""BigMapKeys""")
                .Filter("BigMapPtr", ptr)
                .Filter("Active", active)
                .Filter("JsonKey", key)
                .Filter("JsonValue", value)
                .Take(sort, offset, limit, x => x switch
                {
                    "firstLevel" => ("Id", "FirstLevel"),
                    "lastLevel" => ("LastLevel", "LastLevel"),
                    "updates" => ("Updates", "Updates"),
                    _ => ("Id", "Id")
                });

            using var db = GetConnection();
            var rows = await db.QueryAsync(sql.Query, sql.Params);

            //TODO: optimize memory allocation
            var result = new object[rows.Count()];
            var j = 0;

            switch (field)
            {
                case "active":
                    foreach (var row in rows)
                        result[j++] = row.Active;
                    break;
                case "firstLevel":
                    foreach (var row in rows)
                        result[j++] = row.FirstLevel;
                    break;
                case "lastLevel":
                    foreach (var row in rows)
                        result[j++] = row.LastLevel;
                    break;
                case "updates":
                    foreach (var row in rows)
                        result[j++] = row.Updates;
                    break;
                case "hash":
                    foreach (var row in rows)
                        result[j++] = row.KeyHash;
                    break;
                case "key":
                    foreach (var row in rows)
                        result[j++] = micheline switch
                        {
                            MichelineFormat.Json => new RawJson(row.JsonKey),
                            MichelineFormat.JsonString => row.JsonKey,
                            MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawKey)),
                            MichelineFormat.RawString => Micheline.ToJson(row.RawKey),
                            _ => null
                        };
                    break;
                case "value":
                    foreach (var row in rows)
                        result[j++] = micheline switch
                        {
                            MichelineFormat.Json => new RawJson(row.JsonValue),
                            MichelineFormat.JsonString => row.JsonValue,
                            MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawValue)),
                            MichelineFormat.RawString => Micheline.ToJson(row.RawValue),
                            _ => null
                        };
                    break;
            }

            return result;
        }
        #endregion

        #region bigmap key updates
        public async Task<IEnumerable<BigMapUpdate>> GetKeyUpdates(
            int ptr,
            string key,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            MichelineFormat micheline)
        {
            using var db = GetConnection();
            var keyRow = await db.QueryFirstOrDefaultAsync(@"
                SELECT  ""Id""
                FROM    ""BigMapKeys""
                WHERE   ""BigMapPtr"" = @ptr
                AND     ""JsonKey"" = @key::jsonb
                LIMIT   1",
                new { ptr, key });

            if (keyRow == null) return null;

            var sql = new SqlBuilder(@"SELECT * FROM ""BigMapUpdates""")
                .Filter("BigMapKeyId", (int)keyRow.Id)
                .Take(sort, offset, limit, x => ("Id", "Id"));

            var rows = await db.QueryAsync(sql.Query, sql.Params);
            return rows.Select(row => (BigMapUpdate)ReadBigMapUpdate(row, micheline));
        }

        public async Task<IEnumerable<BigMapUpdate>> GetKeyByHashUpdates(
            int ptr,
            string hash,
            SortParameter sort,
            OffsetParameter offset,
            int limit,
            MichelineFormat micheline)
        {
            using var db = GetConnection();
            var keyRow = await db.QueryFirstOrDefaultAsync(@"
                SELECT  ""Id""
                FROM    ""BigMapKeys""
                WHERE   ""BigMapPtr"" = @ptr
                AND     ""KeyHash"" = @hash::character(54)
                LIMIT   1",
                new { ptr, hash });

            if (keyRow == null) return null;

            var sql = new SqlBuilder(@"SELECT * FROM ""BigMapUpdates""")
                .Filter("BigMapKeyId", (int)keyRow.Id)
                .Take(sort, offset, limit, x => ("Id", "Id"));

            var rows = await db.QueryAsync(sql.Query, sql.Params);
            return rows.Select(row => (BigMapUpdate)ReadBigMapUpdate(row, micheline));
        }
        #endregion

        BigMap ReadBigMap(dynamic row, MichelineFormat format)
        {
            return new BigMap
            {
                Ptr = row.Ptr,
                Contract = Accounts.GetAlias(row.ContractId),
                Path = ((string)row.StoragePath).Replace(".", "..").Replace(',', '.'),
                Active = row.Active,
                FirstLevel = row.FirstLevel,
                LastLevel = row.LastLevel,
                TotalKeys = row.TotalKeys,
                ActiveKeys = row.ActiveKeys,
                Updates = row.Updates,
                KeyType = (int)format < 2
                    ? new RawJson(Schema.Create(Micheline.FromBytes(row.KeyType) as MichelinePrim).Humanize())
                    : new RawJson(Micheline.ToJson(row.KeyType)),
                ValueType = (int)format < 2
                    ? new RawJson(Schema.Create(Micheline.FromBytes(row.ValueType) as MichelinePrim).Humanize())
                    : new RawJson(Micheline.ToJson(row.ValueType))
            };
        }

        BigMapKey ReadBigMapKey(dynamic row, MichelineFormat format)
        {
            return new BigMapKey
            {
                Id = row.Id,
                Active = row.Active,
                FirstLevel = row.FirstLevel,
                LastLevel = row.LastLevel,
                Updates = row.Updates,
                Hash = row.KeyHash,
                Key = format switch
                {
                    MichelineFormat.Json => new RawJson(row.JsonKey),
                    MichelineFormat.JsonString => row.JsonKey,
                    MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawKey)),
                    MichelineFormat.RawString => Micheline.ToJson(row.RawKey),
                    _ => null
                },
                Value = format switch
                {
                    MichelineFormat.Json => new RawJson(row.JsonValue),
                    MichelineFormat.JsonString => row.JsonValue,
                    MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawValue)),
                    MichelineFormat.RawString => Micheline.ToJson(row.RawValue),
                    _ => null
                }
            };
        }

        BigMapUpdate ReadBigMapUpdate(dynamic row, MichelineFormat format)
        {
            return new BigMapUpdate
            {
                Id = row.Id,
                Level = row.Level,
                Timestamp = Times[row.Level],
                Action = UpdateAction((int)row.Action),
                Value = format switch
                {
                    MichelineFormat.Json => new RawJson(row.JsonValue),
                    MichelineFormat.JsonString => row.JsonValue,
                    MichelineFormat.Raw => new RawJson(Micheline.ToJson(row.RawValue)),
                    MichelineFormat.RawString => Micheline.ToJson(row.RawValue),
                    _ => null
                }
            };
        }

        string UpdateAction(int action) => action switch
        {
            1 => BigMapActions.AddKey,
            2 => BigMapActions.UpdateKey,
            3 => BigMapActions.RemoveKey,
            _ => "unknown"
        };
    }
}
