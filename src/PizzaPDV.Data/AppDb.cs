using Microsoft.Data.Sqlite;
using Dapper;

namespace PizzaPDV.Data;

public class AppDb
{
    private readonly string _dbPath;
    private readonly string _connString;

    public AppDb(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pizzapdv.db");
        _connString = $"Data Source={_dbPath}";
        Init();
    }

    public SqliteConnection Connect() => new SqliteConnection(_connString);

    private void Init()
    {
        using var conn = Connect();
        conn.Open();
        conn.Execute("PRAGMA journal_mode=WAL;");

        // Produtos (espelho Supabase)
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS produtos (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL,
                categoria TEXT NOT NULL,
                descricao TEXT,
                ativo INTEGER NOT NULL DEFAULT 1,
                max_sabores INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS variacoes (
                id TEXT PRIMARY KEY,
                produto_id TEXT NOT NULL REFERENCES produtos(id) ON DELETE CASCADE,
                tamanho TEXT NOT NULL,
                fatias INTEGER,
                preco REAL NOT NULL,
                custo REAL NOT NULL DEFAULT 0,
                ativo INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL,
                UNIQUE(produto_id, tamanho)
            );
            CREATE TABLE IF NOT EXISTS sabores (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL UNIQUE,
                categoria TEXT NOT NULL DEFAULT 'salgada',
                custo REAL NOT NULL DEFAULT 0,
                ativo INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS bordas (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL,
                preco_adicional REAL NOT NULL,
                custo REAL NOT NULL DEFAULT 0,
                tipo TEXT NOT NULL,
                ativo INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS bairros (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL UNIQUE,
                taxa_fixa REAL NOT NULL,
                ativo INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS mesas (
                numero INTEGER PRIMARY KEY CHECK(numero BETWEEN 1 AND 20),
                status TEXT NOT NULL DEFAULT 'livre',
                comanda_aberta TEXT,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS pedidos_local (
                id TEXT PRIMARY KEY,
                origem TEXT NOT NULL,
                status TEXT NOT NULL,
                cliente_nome TEXT NOT NULL,
                cliente_telefone TEXT NOT NULL,
                cliente_endereco TEXT,
                bairro_id TEXT,
                taxa_entrega REAL NOT NULL DEFAULT 0,
                mesa_numero INTEGER,
                comanda_id TEXT,
                subtotal REAL NOT NULL,
                total REAL NOT NULL,
                forma_pagamento TEXT NOT NULL,
                status_pagamento TEXT NOT NULL DEFAULT 'pendente',
                observacao TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS itens_pedido_local (
                id TEXT PRIMARY KEY,
                pedido_id TEXT NOT NULL REFERENCES pedidos_local(id) ON DELETE CASCADE,
                produto_id TEXT NOT NULL,
                variacao_id TEXT NOT NULL,
                sabor1_id TEXT,
                sabor2_id TEXT,
                borda_id TEXT,
                quantidade INTEGER NOT NULL,
                observacao TEXT,
                preco_unit REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS outbox (
                id TEXT PRIMARY KEY,
                table_name TEXT NOT NULL,
                op TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                attempts INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS caixa_local (
                id TEXT PRIMARY KEY,
                aberto_em TEXT NOT NULL,
                fechado_em TEXT,
                saldo_inicial REAL NOT NULL,
                total_vendas REAL NOT NULL DEFAULT 0,
                por_forma_json TEXT NOT NULL DEFAULT '{}',
                synced INTEGER NOT NULL DEFAULT 0
            );
        ");

        // Seed se vazio
        var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM bordas");
        if (count == 0) Seed(conn);
    }

    private void Seed(SqliteConnection conn)
    {
        var now = DateTime.UtcNow.ToString("o");

        // Bairros
        conn.Execute("INSERT OR IGNORE INTO bairros (id, nome, taxa_fixa, ativo, created_at, updated_at) VALUES (@id,@nome,@taxa,1,@now,@now)",
            new[] {
                new { id=Guid.NewGuid().ToString(), nome="Centro", taxa=5m, now },
                new { id=Guid.NewGuid().ToString(), nome="Bairro Novo", taxa=7m, now },
                new { id=Guid.NewGuid().ToString(), nome="Sitio Sede", taxa=8m, now },
                new { id=Guid.NewGuid().ToString(), nome="Zona Rural", taxa=10m, now },
                new { id=Guid.NewGuid().ToString(), nome="Bairro Alto", taxa=6m, now },
            });

        // Mesas 1..20
        for (int i=1;i<=20;i++)
            conn.Execute("INSERT OR IGNORE INTO mesas (numero,status,updated_at) VALUES (@n,'livre',@now)", new { n=i, now });

        // Bordas
        conn.Execute("INSERT OR IGNORE INTO bordas (id,nome,preco_adicional,custo,tipo,ativo,updated_at) VALUES (@id,@nome,@preco,@custo,@tipo,1,@now)",
            new[] {
                new { id="catupiry", nome="Catupiry", preco=8m, custo=2.5m, tipo="comum", now },
                new { id="cream_cheese", nome="Cream Cheese", preco=8m, custo=2.8m, tipo="comum", now },
                new { id="chocolate", nome="Chocolate", preco=9m, custo=3m, tipo="comum", now },
                new { id="comum", nome="Borda Comum", preco=5m, custo=1.2m, tipo="comum", now },
                new { id="camarao", nome="Borda Camarao", preco=15m, custo=6m, tipo="premium", now },
                new { id="vulcao", nome="Borda Vulcao", preco=18m, custo=7m, tipo="premium", now },
            });

        // Sabores
        conn.Execute("INSERT OR IGNORE INTO sabores (id,nome,categoria,custo,ativo,updated_at) VALUES (@id,@nome,@cat,@custo,1,@now)",
            new[] {
                new { id=Guid.NewGuid().ToString(), nome="Calabresa", cat="salgada", custo=6m, now },
                new { id=Guid.NewGuid().ToString(), nome="Mussarela", cat="salgada", custo=5.5m, now },
                new { id=Guid.NewGuid().ToString(), nome="Frango c/ Catupiry", cat="salgada", custo=7m, now },
                new { id=Guid.NewGuid().ToString(), nome="Portuguesa", cat="salgada", custo=7.5m, now },
                new { id=Guid.NewGuid().ToString(), nome="Chocolate c/ Morango", cat="doce", custo=8m, now },
            });

        // Produtos + variacoes
        var pG = Guid.NewGuid().ToString();
        var pP = Guid.NewGuid().ToString();
        var pEsfiha = Guid.NewGuid().ToString();
        var pCalzone = Guid.NewGuid().ToString();
        conn.Execute("INSERT OR IGNORE INTO produtos (id,nome,categoria,descricao,ativo,max_sabores,created_at,updated_at) VALUES (@id,@nome,@cat,@desc,1,@max,@now,@now)",
            new[] {
                new { id=pG, nome="Pizza Grande (8 fatias)", cat="pizza", desc="Pizza tamanho G", max=2, now },
                new { id=pP, nome="Pizza Pequena (4 fatias)", cat="pizza", desc="Pizza tamanho P", max=2, now },
                new { id=pEsfiha, nome="Esfiha Aberta", cat="esfiha", desc="Esfiha tradicional", max=1, now },
                new { id=pCalzone, nome="Calzone", cat="calzone", desc="Calzone recheado", max=1, now },
            });
        conn.Execute("INSERT OR IGNORE INTO variacoes (id,produto_id,tamanho,fatias,preco,custo,ativo,updated_at) VALUES (@id,@pid,@tam,@fat,@preco,@custo,1,@now)",
            new[] {
                new { id=Guid.NewGuid().ToString(), pid=pG, tam="G", fat=(int?)8, preco=59.90m, custo=18m, now },
                new { id=Guid.NewGuid().ToString(), pid=pP, tam="P", fat=(int?)4, preco=34.90m, custo=11m, now },
                new { id=Guid.NewGuid().ToString(), pid=pEsfiha, tam="unico", fat=(int?)null, preco=6m, custo=1.8m, now },
                new { id=Guid.NewGuid().ToString(), pid=pCalzone, tam="unico", fat=(int?)null, preco=22m, custo=7m, now },
            });

        // Caixa inicial
        conn.Execute("INSERT OR IGNORE INTO caixa_local (id,aberto_em,saldo_inicial,total_vendas,por_forma_json,synced) VALUES (@id,@now,100,0,'{}',0)",
            new { id=Guid.NewGuid().ToString(), now });
    }

    // Helpers
    public IEnumerable<dynamic> Query(string sql, object? param = null)
    {
        using var conn = Connect();
        conn.Open();
        return conn.Query(sql, param);
    }

    public void Execute(string sql, object? param = null)
    {
        using var conn = Connect();
        conn.Open();
        conn.Execute(sql, param);
    }

    public void EnqueueOutbox(string tableName, string op, string payloadJson)
    {
        using var conn = Connect();
        conn.Open();
        conn.Execute("INSERT INTO outbox (id,table_name,op,payload_json,attempts,created_at) VALUES (@id,@t,@op,@p,0,@now)",
            new { id="out_" + Guid.NewGuid().ToString("N").Substring(0,8), t=tableName, op, p=payloadJson, now=DateTime.UtcNow.ToString("o") });
    }
}
