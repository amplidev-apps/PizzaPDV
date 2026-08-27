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
                updated_at TEXT NOT NULL,
                manipulado INTEGER NOT NULL DEFAULT 0,
                rendimento_porcoes INTEGER,
                peso_total_g REAL,
                custo_calculado REAL,
                modo_preparo TEXT,
                disponivel INTEGER NOT NULL DEFAULT 1
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
            -- Estágio 0 fundação precificação justa + monte sua + validade + divisão
            CREATE TABLE IF NOT EXISTS config (
                chave TEXT PRIMARY KEY,
                valor TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS insumos (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL UNIQUE,
                unidade TEXT NOT NULL CHECK(unidade IN ('g','kg','ml','l','un','col')),
                qtd_embalagem REAL NOT NULL,
                preco_embalagem REAL NOT NULL,
                custo_por_unidade REAL NOT NULL DEFAULT 0,
                tipo TEXT,
                ativo INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS receitas_massa (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL UNIQUE,
                tipo TEXT NOT NULL CHECK(tipo IN ('pizza','esfiha','salgado')),
                peso_total_g REAL NOT NULL DEFAULT 0,
                custo_total REAL NOT NULL DEFAULT 0,
                custo_por_g REAL NOT NULL DEFAULT 0,
                porcao_padrao_g REAL NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS receita_itens (
                id TEXT PRIMARY KEY,
                receita_id TEXT NOT NULL REFERENCES receitas_massa(id) ON DELETE CASCADE,
                insumo_id TEXT NOT NULL REFERENCES insumos(id),
                quantidade REAL NOT NULL,
                unidade TEXT NOT NULL,
                fator_perda REAL NOT NULL DEFAULT 1.0
            );
            CREATE TABLE IF NOT EXISTS bases_tamanho (
                id TEXT PRIMARY KEY,
                tamanho TEXT NOT NULL CHECK(tamanho IN ('P','G','unico')),
                peso_massa_g REAL NOT NULL,
                custo_massa REAL NOT NULL DEFAULT 0,
                custo_fixos REAL NOT NULL DEFAULT 0,
                custo_total REAL NOT NULL DEFAULT 0,
                preco_exibido REAL NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL,
                UNIQUE(tamanho)
            );
            CREATE TABLE IF NOT EXISTS sabor_insumos (
                id TEXT PRIMARY KEY,
                sabor_id TEXT NOT NULL REFERENCES sabores(id) ON DELETE CASCADE,
                insumo_id TEXT NOT NULL REFERENCES insumos(id),
                quantidade REAL NOT NULL,
                unidade TEXT NOT NULL,
                forma_corte TEXT CHECK(forma_corte IN ('fatiado','cubos','moido','espremido','picado','inteiro','rodelas','ralado')),
                fator_perda REAL NOT NULL DEFAULT 1.0,
                observacao TEXT,
                UNIQUE(sabor_id, insumo_id, forma_corte)
            );
            CREATE TABLE IF NOT EXISTS recheios_porcao (
                id TEXT PRIMARY KEY,
                sabor_id TEXT NOT NULL REFERENCES sabores(id) ON DELETE CASCADE,
                peso_g REAL NOT NULL DEFAULT 120,
                preco_sugerido REAL NOT NULL DEFAULT 0,
                disponivel INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS clientes_local (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL,
                telefone TEXT NOT NULL UNIQUE,
                bairro_id TEXT,
                enderecos_json TEXT NOT NULL DEFAULT '[]',
                total_pizzas_g INTEGER NOT NULL DEFAULT 0,
                pizzas_g_para_fidelidade INTEGER NOT NULL DEFAULT 0,
                cupons_pendentes INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS comandas (
                id TEXT PRIMARY KEY,
                mesa_numero INTEGER NOT NULL REFERENCES mesas(numero),
                status TEXT NOT NULL DEFAULT 'aberta' CHECK(status IN ('aberta','fechada','paga')),
                total REAL NOT NULL DEFAULT 0,
                total_pago REAL NOT NULL DEFAULT 0,
                desconto REAL NOT NULL DEFAULT 0,
                taxa_servico REAL NOT NULL DEFAULT 0,
                n_pessoas INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS pagamentos_comanda (
                id TEXT PRIMARY KEY,
                comanda_id TEXT NOT NULL REFERENCES comandas(id) ON DELETE CASCADE,
                mesa_numero INTEGER NOT NULL,
                valor REAL NOT NULL,
                forma_pagamento TEXT NOT NULL,
                status_pagamento TEXT NOT NULL DEFAULT 'pago',
                pessoa_idx INTEGER,
                pessoa_nome TEXT,
                troco REAL NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS produtos_manipulados (
                id TEXT PRIMARY KEY,
                nome TEXT NOT NULL,
                categoria TEXT NOT NULL DEFAULT 'manipulado',
                data_manipulacao TEXT NOT NULL,
                data_validade TEXT NOT NULL,
                dias_validade INTEGER NOT NULL DEFAULT 3,
                responsavel TEXT NOT NULL,
                quantidade REAL NOT NULL DEFAULT 1,
                unidade TEXT NOT NULL DEFAULT 'un',
                lote TEXT,
                observacao TEXT,
                status TEXT NOT NULL DEFAULT 'valido' CHECK(status IN ('valido','vencendo','vencido','descartado','consumido')),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_manipulados_validade ON produtos_manipulados(data_validade);
            CREATE INDEX IF NOT EXISTS idx_manipulados_status ON produtos_manipulados(status);
            CREATE INDEX IF NOT EXISTS idx_sabor_insumos_sabor ON sabor_insumos(sabor_id);
            CREATE INDEX IF NOT EXISTS idx_receita_itens_receita ON receita_itens(receita_id);
            -- WhatsApp terreno pré-configurado
            CREATE TABLE IF NOT EXISTS conversas_whatsapp (
                tel TEXT PRIMARY KEY,
                nome TEXT,
                estado TEXT NOT NULL DEFAULT 'saudacao' CHECK(estado IN ('saudacao','cadastro','menu','pizza_tamanho','sabor1','sabor2','borda','adicionais','mais_itens','bairro','endereco','pagamento','aguardando_comprovante','finalizado')),
                carrinho_json TEXT NOT NULL DEFAULT '[]',
                bairro_id TEXT,
                endereco TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS whatsapp_mensagens (
                id TEXT PRIMARY KEY,
                tel TEXT NOT NULL REFERENCES conversas_whatsapp(tel),
                direcao TEXT NOT NULL CHECK(direcao IN ('in','out')),
                tipo TEXT NOT NULL,
                body TEXT,
                payload_json TEXT,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_whatsapp_mensagens_tel ON whatsapp_mensagens(tel);
        ");

        // Migração para DBs antigos (adiciona colunas faltantes sem falhar)
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN manipulado INTEGER NOT NULL DEFAULT 0"); } catch {}
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN rendimento_porcoes INTEGER"); } catch {}
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN peso_total_g REAL"); } catch {}
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN custo_calculado REAL"); } catch {}
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN modo_preparo TEXT"); } catch {}
        try { conn.Execute("ALTER TABLE sabores ADD COLUMN disponivel INTEGER NOT NULL DEFAULT 1"); } catch {}
        try { conn.Execute("ALTER TABLE itens_pedido_local ADD COLUMN adicionais_json TEXT"); } catch {}
        try { conn.Execute("ALTER TABLE pedidos_local ADD COLUMN valor_pago REAL DEFAULT 0"); } catch {}

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

        // Config padrão
        conn.Execute("INSERT OR IGNORE INTO config (chave,valor,updated_at) VALUES ('margem_padrao','60',@now)", new { now });
        conn.Execute("INSERT OR IGNORE INTO config (chave,valor,updated_at) VALUES ('taxa_servico','0',@now)", new { now });
        conn.Execute("INSERT OR IGNORE INTO config (chave,valor,updated_at) VALUES ('comissao_tipo','taxa',@now)", new { now });

        // Insumos base (precificação justa)
        var insumosSeed = new[]
        {
            new { id=Guid.NewGuid().ToString(), nome="Farinha de Trigo", unidade="kg", qtd=1m, preco=5.00m, custo=5.00m },
            new { id=Guid.NewGuid().ToString(), nome="Açúcar", unidade="kg", qtd=1m, preco=4.50m, custo=4.50m },
            new { id=Guid.NewGuid().ToString(), nome="Sal", unidade="kg", qtd=1m, preco=2.00m, custo=2.00m },
            new { id=Guid.NewGuid().ToString(), nome="Fermento", unidade="kg", qtd=0.1m, preco=8.00m, custo=80.00m },
            new { id=Guid.NewGuid().ToString(), nome="Óleo", unidade="l", qtd=0.9m, preco=8.00m, custo=8.88m },
            new { id=Guid.NewGuid().ToString(), nome="Ovos", unidade="un", qtd=30m, preco=18.00m, custo=0.60m },
            new { id=Guid.NewGuid().ToString(), nome="Margarina", unidade="col", qtd=1m, preco=0.50m, custo=0.50m },
            new { id=Guid.NewGuid().ToString(), nome="Leite", unidade="l", qtd=1m, preco=5.00m, custo=5.00m },
            new { id=Guid.NewGuid().ToString(), nome="Água", unidade="l", qtd=1m, preco=0m, custo=0m },
            new { id=Guid.NewGuid().ToString(), nome="Caldo Galinha pó", unidade="kg", qtd=0.05m, preco=4.00m, custo=80.00m },
            new { id=Guid.NewGuid().ToString(), nome="Farinha Panko", unidade="kg", qtd=1m, preco=12.00m, custo=12.00m },
            new { id=Guid.NewGuid().ToString(), nome="Mussarela peça", unidade="kg", qtd=4m, preco=42.00m, custo=10.50m },
            new { id=Guid.NewGuid().ToString(), nome="Tomate", unidade="un", qtd=1m, preco=0.80m, custo=0.80m },
            new { id=Guid.NewGuid().ToString(), nome="Cebola", unidade="un", qtd=1m, preco=0.60m, custo=0.60m },
            new { id=Guid.NewGuid().ToString(), nome="Carne moída", unidade="kg", qtd=1m, preco=32.00m, custo=32.00m },
            new { id=Guid.NewGuid().ToString(), nome="Pimenta do reino", unidade="g", qtd=100m, preco=10.00m, custo=0.10m },
            new { id=Guid.NewGuid().ToString(), nome="Limão Tahiti", unidade="un", qtd=1m, preco=0.50m, custo=0.50m },
            new { id=Guid.NewGuid().ToString(), nome="Embalagem pequena", unidade="un", qtd=30m, preco=25.00m, custo=0.83m },
            new { id=Guid.NewGuid().ToString(), nome="Orégano", unidade="g", qtd=100m, preco=8.00m, custo=0.08m },
            new { id=Guid.NewGuid().ToString(), nome="Azeitona", unidade="un", qtd=1m, preco=0.05m, custo=0.05m },
        };
        foreach (var ins in insumosSeed)
        {
            conn.Execute("INSERT OR IGNORE INTO insumos (id,nome,unidade,qtd_embalagem,preco_embalagem,custo_por_unidade,ativo,updated_at) VALUES (@id,@nome,@unidade,@qtd,@preco,@custo,1,@now)",
                new { id=ins.id, nome=ins.nome, unidade=ins.unidade, qtd=ins.qtd, preco=ins.preco, custo=ins.custo, now });
        }

        // Receitas massa
        var massaPizzaId = Guid.NewGuid().ToString();
        var massaEsfihaId = Guid.NewGuid().ToString();
        var massaSalgadoId = Guid.NewGuid().ToString();
        conn.Execute("INSERT OR IGNORE INTO receitas_massa (id,nome,tipo,peso_total_g,custo_total,custo_por_g,porcao_padrao_g,updated_at) VALUES (@id,@nome,@tipo,@peso,@custo,@cpg,@porcao,@now)",
            new[] {
                new { id=massaPizzaId, nome="Massa Pizza", tipo="pizza", peso=1450m, custo=12.30m, cpg=0.00848m, porcao=320m, now },
                new { id=massaEsfihaId, nome="Massa Esfiha", tipo="esfiha", peso=1200m, custo=10.50m, cpg=0.00875m, porcao=80m, now },
                new { id=massaSalgadoId, nome="Massa Salgados", tipo="salgado", peso=2100m, custo=18.00m, cpg=0.00857m, porcao=100m, now },
            });

        // Bases tamanho (massa + fixos invisíveis)
        conn.Execute("INSERT OR IGNORE INTO bases_tamanho (id,tamanho,peso_massa_g,custo_massa,custo_fixos,custo_total,preco_exibido,updated_at) VALUES (@id,@tam,@peso,@cm,@cf,@ct,@pr,@now)",
            new[] {
                new { id=Guid.NewGuid().ToString(), tam="P", peso=180m, cm=1.52m, cf=1.28m, ct=2.80m, pr=9.90m, now },
                new { id=Guid.NewGuid().ToString(), tam="G", peso=320m, cm=2.71m, cf=1.89m, ct=4.60m, pr=14.90m, now },
            });
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
