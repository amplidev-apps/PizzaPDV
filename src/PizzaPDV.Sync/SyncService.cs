using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PizzaPDV.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace PizzaPDV.Sync;

public class SyncService
{
    private readonly AppDb _db;
    private readonly HttpClient _http;
    private readonly string? _supabaseUrl;
    private readonly string? _supabaseKey;

    public SyncService(AppDb db, string? supabaseUrl = null, string? supabaseKey = null)
    {
        _db = db;
        _supabaseUrl = supabaseUrl ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
        _supabaseKey = supabaseKey ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        _http = new HttpClient();
        if (!string.IsNullOrEmpty(_supabaseKey))
            _http.DefaultRequestHeaders.Add("apikey", _supabaseKey);
        if (!string.IsNullOrEmpty(_supabaseKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_supabaseUrl) && !string.IsNullOrEmpty(_supabaseKey);

    // Enqueue genérico (usado pela Central para qualquer tabela)
    public void Enqueue(string tableName, string op, object payload)
        => _db.EnqueueOutbox(tableName, op, JsonSerializer.Serialize(payload));

    // Push outbox para Supabase (chamado a cada 10s se online)
    public async Task<(int synced, int pending, string? error)> SyncOutboxAsync()
    {
        if (!IsConfigured) return (0, 1, "Supabase não configurado — offline puro");

        using var conn = _db.Connect();
        conn.Open();
        var rows = conn.Query("SELECT id, table_name as TableName, op as Op, payload_json as Payload FROM outbox ORDER BY created_at").ToList();
        int synced = 0;
        string? lastError = null;

        foreach (var r in rows)
        {
            string table = r.TableName;
            string op = r.Op;
            string payloadJson = r.Payload;
            string id = r.id;
            try
            {
                // Mapeia tabela local -> tabela Supabase (produtos_local -> produtos etc)
                var remoteTable = table.Replace("_local","").Replace("produtos","produtos").Trim();
                // Para catálogo, usa upsert
                if (new[] { "produtos","variacoes","bordas","bairros","sabores","produtos_local","variacoes_local","bordas_local","bairros_local","sabores_local" }.Contains(table))
                {
                    // Upsert precisa de Prefer header
                    var json = payloadJson;
                    // Remove campos locais synced/payload_json se houver
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/{remoteTable}");
                    req.Headers.Add("Prefer", "resolution=merge-duplicates");
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        throw new Exception($"{res.StatusCode}: {body}");
                    }
                }
                else if (table == "pedidos_local" || table == "pedidos")
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/pedidos");
                    req.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        // Se já existe (duplicado), considera sync
                        if (!body.Contains("duplicate")) throw new Exception(body);
                    }
                }
                else
                {
                    // genérico
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/{remoteTable}");
                    req.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    var res = await _http.SendAsync(req);
                    if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync());
                }

                conn.Execute("DELETE FROM outbox WHERE id=@id", new { id });
                synced++;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                conn.Execute("UPDATE outbox SET attempts = attempts + 1 WHERE id=@id", new { id });
            }
        }
        var pending = rows.Count - synced;
        return (synced, pending, lastError);
    }

    // Pull catálogo do Supabase para local (executado ao iniciar e quando online)
    public async Task PullCatalogAsync()
    {
        if (!IsConfigured) return;
        foreach (var table in new[] { "produtos","variacoes","bordas","bairros","sabores" })
        {
            try
            {
                var res = await _http.GetAsync($"{_supabaseUrl}/rest/v1/{table}?select=*");
                if (!res.IsSuccessStatusCode) continue;
                var json = await res.Content.ReadAsStringAsync();
                var docs = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
                if (docs == null) continue;
                using var conn = _db.Connect();
                conn.Open();
                foreach (var doc in docs)
                {
                    // Upsert local — simplificado: delete+insert
                    var id = doc.ContainsKey("id") ? doc["id"].GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                    // Para cada tabela, faz insert or replace
                    if (table == "produtos")
                    {
                        var nome = doc["nome"].GetString()!;
                        var cat = doc["categoria"].GetString()!;
                        var desc = doc.ContainsKey("descricao") && doc["descricao"].ValueKind != JsonValueKind.Null ? doc["descricao"].GetString() : null;
                        var ativo = doc.ContainsKey("ativo") ? doc["ativo"].GetBoolean() : true;
                        var max = doc.ContainsKey("max_sabores") ? doc["max_sabores"].GetInt32() : 1;
                        var updated = doc.ContainsKey("updated_at") ? doc["updated_at"].GetString() ?? DateTime.UtcNow.ToString("o") : DateTime.UtcNow.ToString("o");
                        var created = doc.ContainsKey("created_at") ? doc["created_at"].GetString() ?? updated : updated;
                        conn.Execute("INSERT OR REPLACE INTO produtos (id,nome,categoria,descricao,ativo,max_sabores,created_at,updated_at) VALUES (@id,@nome,@cat,@desc,@ativo,@max,@created,@updated)",
                            new { id, nome, cat, desc, ativo = ativo?1:0, max, created, updated });
                    }
                    else if (table == "bordas")
                    {
                        var nome = doc["nome"].GetString()!;
                        var preco = doc["preco_adicional"].GetDecimal();
                        var custo = doc.ContainsKey("custo") ? doc["custo"].GetDecimal() : 0m;
                        var tipo = doc["tipo"].GetString()!;
                        var ativo = doc.ContainsKey("ativo") ? doc["ativo"].GetBoolean() : true;
                        var updated = doc.ContainsKey("updated_at") ? doc["updated_at"].GetString() ?? DateTime.UtcNow.ToString("o") : DateTime.UtcNow.ToString("o");
                        conn.Execute("INSERT OR REPLACE INTO bordas (id,nome,preco_adicional,custo,tipo,ativo,updated_at) VALUES (@id,@nome,@preco,@custo,@tipo,@ativo,@updated)",
                            new { id, nome, preco, custo, tipo, ativo = ativo?1:0, updated });
                    }
                }
            }
            catch { /* offline ignora */ }
        }
    }
}
