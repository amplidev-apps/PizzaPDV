using PizzaPDV.Data;
using PizzaPDV.Printer;
using PizzaPDV.Sync;
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PizzaPDV.Central;

public class MainForm : Form
{
    private readonly AppDb _db = new AppDb();
    private readonly SyncService _sync;
    private Label lblStatus = null!;
    private Label lblSync = null!;
    private Panel pnlMain = null!;
    private System.Windows.Forms.Timer syncTimer = null!;

    // Classic palette: PDV batata — lots of contrast, flat
    private readonly Color Preto = Color.FromArgb(30, 30, 30);
    private readonly Color Vermelho = Color.FromArgb(180, 30, 30);
    private readonly Color Amarelo = Color.FromArgb(255, 200, 0);
    private readonly Color CinzaClaro = Color.FromArgb(240, 240, 240);

    public MainForm()
    {
        _sync = new SyncService(_db,
            Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "",
            Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? "");

        Text = "PizzaPDV Central — Caixa & Pedidos [F1 Pedidos | F2 Mesas | F3 Cardapio | F4 Caixa | F5 Clientes | F12 Imprimir] — SynX 10 Batata";
        WindowState = FormWindowState.Maximized;
        BackColor = CinzaClaro;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);

        BuildUI();
        LoadPedidos();

        // Sync a cada 10s
        syncTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        syncTimer.Tick += async (_, __) => await DoSync();
        syncTimer.Start();
        _ = DoSync();
    }

    private void BuildUI()
    {
        // Topo preto — status
        var top = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Preto };
        var lblTitle = new Label { Text = "  PIZZAPDV CENTRAL", ForeColor = Color.White, Font = new Font("Consolas", 11, FontStyle.Bold), Dock = DockStyle.Left, AutoSize = false, Width = 220, TextAlign = ContentAlignment.MiddleLeft };
        lblStatus = new Label { Text = "● ONLINE", ForeColor = Color.Lime, Dock = DockStyle.Right, Width = 120, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Consolas", 9, FontStyle.Bold) };
        lblSync = new Label { Text = "JP-58H 58mm | Caixa ABERTO | F1-F5", ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        top.Controls.Add(lblSync);
        top.Controls.Add(lblStatus);
        top.Controls.Add(lblTitle);
        Controls.Add(top);

        // Esquerda — botões F-keys clássicos
        var left = new Panel { Dock = DockStyle.Left, Width = 150, BackColor = Color.FromArgb(45, 45, 45) };
        var btnF1 = MakeMenuButton("F1  PEDIDOS", () => LoadPedidos(), Vermelho);
        var btnF2 = MakeMenuButton("F2  MESAS 1-20", () => LoadMesas(), Color.FromArgb(60,60,60));
        var btnF3 = MakeMenuButton("F3  CARDAPIO", () => LoadCardapio(), Color.FromArgb(60,60,60));
        var btnF4 = MakeMenuButton("F4  CAIXA", () => LoadCaixa(), Color.FromArgb(60,60,60));
        var btnF5 = MakeMenuButton("F5  CLIENTES", () => LoadClientes(), Color.FromArgb(60,60,60));
        var btnF12 = MakeMenuButton("F12 IMPRIMIR", () => TestPrint(), Amarelo, Color.Black);
        // Dock Top empilha inverso, adiciona em ordem reversa para F1 ficar no topo
        left.Controls.Add(btnF12);
        left.Controls.Add(btnF5);
        left.Controls.Add(btnF4);
        left.Controls.Add(btnF3);
        left.Controls.Add(btnF2);
        left.Controls.Add(btnF1);
        foreach (Control c in left.Controls) c.Dock = DockStyle.Top;
        Controls.Add(left);

        pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = CinzaClaro, Padding = new Padding(8) };
        Controls.Add(pnlMain);

        // Bottom
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Preto };
        var lblBottom = new Label { Text = "  SynX 10 Batata — Core Duo DDR2 4GB ✓  |  Offline salva local e sincroniza quando volta  |  Taxa fixa por bairro → comissão entregador", ForeColor = Color.WhiteSmoke, Dock = DockStyle.Fill, Font = new Font("Consolas", 8) };
        bottom.Controls.Add(lblBottom);
        Controls.Add(bottom);

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) LoadPedidos();
            if (e.KeyCode == Keys.F2) LoadMesas();
            if (e.KeyCode == Keys.F3) LoadCardapio();
            if (e.KeyCode == Keys.F4) LoadCaixa();
            if (e.KeyCode == Keys.F5) LoadClientes();
            if (e.KeyCode == Keys.F12) TestPrint();
        };
    }

    private Button MakeMenuButton(string text, Action onClick, Color back, Color? fore = null)
    {
        var b = new Button
        {
            Name = text,
            Text = "  " + text,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore ?? Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 9, FontStyle.Bold),
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, __) => onClick();
        return b;
    }

    private async Task DoSync()
    {
        lblStatus.Text = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() ? "● ONLINE" : "● OFFLINE";
        lblStatus.ForeColor = lblStatus.Text.Contains("ONLINE") ? Color.Lime : Color.OrangeRed;
        if (!_sync.IsConfigured) { lblSync.Text = "Offline puro (sem SUPABASE_URL) — dados locais | JP-58H pronta"; return; }
        var (synced, pending, err) = await _sync.SyncOutboxAsync();
        lblSync.Text = $"Sync: {synced} enviados, {pending} pendentes {(err!=null? $"| {err[..Math.Min(60,err.Length)]}":"")} | JP-58H 58mm";
    }

    // === Pedidos (Kanban simples DataGrid) ===
    private void LoadPedidos()
    {
        pnlMain.Controls.Clear();
        var title = new Label { Text = "PEDIDOS — F1 | Kanban: Recebido → Preparo → Pronto → Entregue", Dock = DockStyle.Top, Height = 24, Font = new Font("Consolas", 10, FontStyle.Bold) };
        pnlMain.Controls.Add(title);

        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Consolas", 9)
        };
        pnlMain.Controls.Add(dgv);

        // Carrega pedidos_local
        using var conn = _db.Connect();
        conn.Open();
        var rows = conn.Query("SELECT id, origem, status, cliente_nome, total, mesa_numero, created_at FROM pedidos_local ORDER BY created_at DESC LIMIT 100").ToList();
        var dt = new DataTable();
        dt.Columns.Add("ID", typeof(string));
        dt.Columns.Add("Origem", typeof(string));
        dt.Columns.Add("Status", typeof(string));
        dt.Columns.Add("Cliente", typeof(string));
        dt.Columns.Add("Mesa", typeof(string));
        dt.Columns.Add("Total", typeof(string));
        foreach (var r in rows)
        {
            dt.Rows.Add((string)r.id, (string)r.origem, (string)r.status, (string)r.cliente_nome, r.mesa_numero?.ToString() ?? "-", $"R$ {Convert.ToDecimal(r.total):F2}");
        }
        // mock se vazio
        if (dt.Rows.Count == 0)
        {
            dt.Rows.Add("a1b2", "site", "recebido", "João (Centro)", "-", "R$ 64,90");
            dt.Rows.Add("c3d4", "mesa", "preparo", "Mesa 07", "07", "R$ 42,00");
        }
        dgv.DataSource = dt;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4) };
        bar.Controls.Add(MakeAction("Aceitar (→ Preparo)", () => MoveSelected(dgv, "preparo")));
        bar.Controls.Add(MakeAction("Pronto", () => MoveSelected(dgv, "pronto")));
        bar.Controls.Add(MakeAction("Entregar", () => MoveSelected(dgv, "entregue")));
        bar.Controls.Add(MakeAction("Cozinha (F12)", () => PrintSelected(dgv, "cozinha"), Amarelo, Color.Black));
        bar.Controls.Add(MakeAction("Cliente", () => PrintSelected(dgv, "cliente")));
        pnlMain.Controls.Add(bar);
    }

    private void MoveSelected(DataGridView dgv, string novoStatus)
    {
        if (dgv.SelectedRows.Count == 0) { MessageBox.Show("Selecione um pedido"); return; }
        var id = dgv.SelectedRows[0].Cells[0].Value?.ToString();
        using var conn = _db.Connect();
        conn.Open();
        conn.Execute("UPDATE pedidos_local SET status=@s, updated_at=@now WHERE id=@id", new { s = novoStatus, now = DateTime.UtcNow.ToString("o"), id });
        // Fidelidade: se entregue, conta pizzas G (simplificado: busca payload_json)
        if (novoStatus == "entregue")
        {
            // Aqui incrementa cliente se houver — mock
        }
        LoadPedidos();
    }

    private void PrintSelected(DataGridView dgv, string tipo)
    {
        if (dgv.SelectedRows.Count == 0) { MessageBox.Show("Selecione um pedido"); return; }
        var id = dgv.SelectedRows[0].Cells[0].Value?.ToString() ?? "mock1234";
        var p = new PedidoPrint(id, "site", "Cliente Teste", "88999990000", "Rua Centro 123", "Centro", 5, null, null,
            new List<ItemPrint> { new ItemPrint("Pizza G Calabresa/Mussarela", 1, 59.90m, "Borda Catupiry", null) },
            59.90m, 64.90m, "pix", DateTime.Now.ToString("HH:mm"), null);
        string raw = tipo == "cozinha" ? Templates.ComandaCozinha(p) : tipo == "delivery" ? Templates.TicketDelivery(p) : Templates.TicketCliente(p);
        var (ok, via) = RawPrinter.PrintAuto(raw);
        MessageBox.Show(ok ? $"Impresso via {via}" : via, "JP-58H 58mm", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    // === Mesas 1-20 ===
    private void LoadMesas()
    {
        pnlMain.Controls.Clear();
        var title = new Label { Text = "MESAS 1-20 — F2 | Clique para abrir comanda | Comanda cozinha sai com MESA marcada", Dock = DockStyle.Top, Height = 24, Font = new Font("Consolas", 10, FontStyle.Bold) };
        pnlMain.Controls.Add(title);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 4 };
        for (int i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        using var conn = _db.Connect();
        conn.Open();
        var mesas = conn.Query("SELECT numero, status FROM mesas ORDER BY numero").ToList();
        if (mesas.Count == 0) // fallback
            mesas = Enumerable.Range(1, 20).Select(n => new { numero = n, status = n <= 2 ? "ocupada" : "livre" }).Cast<dynamic>().ToList();

        foreach (var m in mesas)
        {
            int num = (int)m.numero;
            string status = (string)m.status;
            var btn = new Button
            {
                Text = $"MESA {num:D2}\n{status.ToUpper()}",
                Font = new Font("Consolas", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = status == "livre" ? Color.White : status == "ocupada" ? Color.Gold : Color.OrangeRed,
                ForeColor = Color.Black,
                Margin = new Padding(6),
            };
            btn.FlatAppearance.BorderColor = Color.Gray;
            btn.Click += (_, __) => AbrirMesa(num);
            grid.Controls.Add(btn);
        }
        pnlMain.Controls.Add(grid);
    }

    private void AbrirMesa(int numero)
    {
        var f = new Form
        {
            Text = $"Comanda — Mesa {numero:D2}",
            Width = 560,
            Height = 420,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        var lbl = new Label { Text = $"Mesa {numero:D2} — Adicione itens (P/G meia-a-meia, esfihas, bordas)", Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 4, 0, 0) };
        f.Controls.Add(lbl);
        var lst = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
        f.Controls.Add(lst);

        // Carrega produtos para combo
        using var conn = _db.Connect();
        conn.Open();
        var prods = conn.Query("SELECT id, nome FROM produtos WHERE ativo=1").ToList();
        var cbProd = new ComboBox { Dock = DockStyle.Top, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var p in prods) cbProd.Items.Add($"{p.nome}");
        if (cbProd.Items.Count == 0) { cbProd.Items.Add("Pizza Grande (8 fatias) - R$ 59,90"); cbProd.Items.Add("Pizza Pequena (4 fatias) - R$ 34,90"); }
        cbProd.SelectedIndex = 0;
        f.Controls.Add(cbProd);

        var cbBorda = new ComboBox { Dock = DockStyle.Top, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList };
        cbBorda.Items.AddRange(new object[] { "Sem borda", "Catupiry +R$8", "Cream Cheese +R$8", "Chocolate +R$9", "Borda Comum +R$5", "Borda Camarao +R$15", "Borda Vulcao +R$18" });
        cbBorda.SelectedIndex = 0;
        f.Controls.Add(cbBorda);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        bar.Controls.Add(new Button { Text = "Fechar", DialogResult = DialogResult.Cancel, Width = 80 });
        var btnEnviar = new Button { Text = "Enviar p/ Cozinha (F12)", BackColor = Vermelho, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 160 };
        btnEnviar.Click += (_, __) =>
        {
            var prod = cbProd.SelectedItem?.ToString() ?? "Pizza";
            var borda = cbBorda.SelectedItem?.ToString() ?? "";
            lst.Items.Add($"1x {prod} {(borda.Contains("Sem")? "" : "+ "+borda)}");
            // Salva pedido_local + imprime
            var id = "tmp_" + Guid.NewGuid().ToString("N")[..8];
            var now = DateTime.UtcNow.ToString("o");
            conn.Execute("INSERT INTO pedidos_local (id,origem,status,cliente_nome,cliente_telefone,subtotal,total,forma_pagamento,mesa_numero,created_at,updated_at,payload_json,synced) VALUES (@id,'mesa','recebido',@cli,@tel,@sub,@tot,'dinheiro',@mesa,@now,@now,@pay,0)",
                new { id, cli = $"Mesa {numero:D2}", tel = "00000000000", sub = 59.90m, tot = 64.90m, mesa = numero, now, pay = $"{{\"id\":\"{id}\",\"mesa\":{numero}}}" });
            conn.Execute("UPDATE mesas SET status='ocupada', updated_at=@now WHERE numero=@n", new { now, n = numero });
            _sync.Enqueue("pedidos", "insert", new { id, origem = "mesa", status = "recebido", cliente_nome = $"Mesa {numero:D2}", mesa_numero = numero, total = 64.90m });

            var p = new PedidoPrint(id, "mesa", $"Mesa {numero:D2}", "-", null, null, 0, numero, null,
                new List<ItemPrint> { new ItemPrint(prod, 1, 59.90m, borda, null) }, 59.90m, 59.90m, "mesa", DateTime.Now.ToString("HH:mm"), null);
            var raw = Templates.ComandaCozinha(p);
            var (ok, via) = RawPrinter.PrintAuto(raw);
            MessageBox.Show(ok ? $"Comanda enviada via {via}" : via);
        };
        bar.Controls.Add(btnEnviar);
        var btnAdd = new Button { Text = "Adicionar", Width = 90 };
        btnAdd.Click += (_, __) => lst.Items.Add($"1x {cbProd.SelectedItem} + {cbBorda.SelectedItem}");
        bar.Controls.Add(btnAdd);
        f.Controls.Add(bar);

        f.ShowDialog(this);
        LoadMesas();
    }

    // === Cardápio (Central dona) ===
    private void LoadCardapio()
    {
        pnlMain.Controls.Clear();
        var title = new Label { Text = "CARDAPIO — F3 | Central edita aqui e sincroniza com Site (Supabase → Realtime)", Dock = DockStyle.Top, Height = 24, Font = new Font("Consolas", 10, FontStyle.Bold), ForeColor = Vermelho };
        pnlMain.Controls.Add(title);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(MakeProdutosTab());
        tabs.TabPages.Add(MakeBordasTab());
        tabs.TabPages.Add(MakeBairrosTab());
        tabs.TabPages.Add(MakeSaboresTab());
        pnlMain.Controls.Add(tabs);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 32 };
        var btnSync = new Button { Text = "Sincronizar agora com Site →", BackColor = Vermelho, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Right, Width = 220 };
        btnSync.Click += async (_, __) => { await DoSync(); MessageBox.Show("Fila enviada. Site atualiza em segundos via Realtime."); };
        bar.Controls.Add(btnSync);
        pnlMain.Controls.Add(bar);
    }

    private TabPage MakeProdutosTab()
    {
        var tp = new TabPage("Produtos (P/G/unico)");
        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Consolas", 9) };
        tp.Controls.Add(dgv);
        using var conn = _db.Connect();
        conn.Open();
        var rows = conn.Query(@"SELECT p.nome, p.categoria, v.tamanho, v.preco, v.custo FROM produtos p LEFT JOIN variacoes v ON v.produto_id=p.id WHERE p.ativo=1 ORDER BY p.nome").ToList();
        var dt = new DataTable();
        dt.Columns.Add("Produto"); dt.Columns.Add("Categoria"); dt.Columns.Add("Tam"); dt.Columns.Add("Preço"); dt.Columns.Add("Custo");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.categoria, (string)(r.tamanho ?? "-"), $"R$ {Convert.ToDecimal(r.preco):F2}", $"R$ {Convert.ToDecimal(r.custo):F2}");
        if (dt.Rows.Count == 0) { dt.Rows.Add("Pizza Grande (8 fatias)", "pizza", "G", "R$ 59,90", "R$ 18,00"); dt.Rows.Add("Pizza Pequena (4 fatias)", "pizza", "P", "R$ 34,90", "R$ 11,00"); }
        dgv.DataSource = dt;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
        bar.Controls.Add(MakeAction("Novo Produto", () => NovoProduto()));
        bar.Controls.Add(MakeAction("Editar Preço", () => EditarPreco(dgv)));
        bar.Controls.Add(MakeAction("Desativar", () => ToggleProduto(dgv)));
        tp.Controls.Add(bar);
        return tp;
    }

    private TabPage MakeBordasTab()
    {
        var tp = new TabPage("Bordas");
        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Consolas", 9) };
        tp.Controls.Add(dgv);
        using var conn = _db.Connect();
        conn.Open();
        var rows = conn.Query("SELECT id, nome, preco_adicional, tipo FROM bordas WHERE ativo=1 ORDER BY preco_adicional").ToList();
        var dt = new DataTable(); dt.Columns.Add("ID"); dt.Columns.Add("Nome"); dt.Columns.Add("Preço"); dt.Columns.Add("Tipo");
        foreach (var r in rows) dt.Rows.Add((string)r.id, (string)r.nome, $"R$ {Convert.ToDecimal(r.preco_adicional):F2}", (string)r.tipo);
        dgv.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
        bar.Controls.Add(MakeAction("Editar Borda", () => EditarBorda(dgv)));
        tp.Controls.Add(bar);
        return tp;
    }

    private TabPage MakeBairrosTab()
    {
        var tp = new TabPage("Bairros (taxa fixa)");
        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Consolas", 9) };
        tp.Controls.Add(dgv);
        using var conn = _db.Connect(); conn.Open();
        var rows = conn.Query("SELECT nome, taxa_fixa FROM bairros WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Bairro"); dt.Columns.Add("Taxa");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, $"R$ {Convert.ToDecimal(r.taxa_fixa):F2}");
        dgv.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
        bar.Controls.Add(MakeAction("Novo Bairro", () => NovoBairro()));
        bar.Controls.Add(MakeAction("Editar Taxa", () => EditarBairro(dgv)));
        tp.Controls.Add(bar);
        return tp;
    }

    private TabPage MakeSaboresTab()
    {
        var tp = new TabPage("Sabores");
        var dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Font = new Font("Consolas", 9) };
        tp.Controls.Add(dgv);
        using var conn = _db.Connect(); conn.Open();
        var rows = conn.Query("SELECT nome, categoria, custo FROM sabores WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Sabor"); dt.Columns.Add("Cat"); dt.Columns.Add("Custo");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.categoria, $"R$ {Convert.ToDecimal(r.custo):F2}");
        dgv.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
        bar.Controls.Add(MakeAction("Novo Sabor", () => NovoSabor()));
        tp.Controls.Add(bar);
        return tp;
    }

    private void NovoProduto()
    {
        var nome = Prompt("Nome do produto:", "Pizza Grande (8 fatias)");
        if (string.IsNullOrWhiteSpace(nome)) return;
        var cat = Prompt("Categoria (pizza/esfiha/calzone/salgado/pastel/bebida):", "pizza");
        var preco = Prompt("Preço (ex: 59.90):", "59.90");
        if (!decimal.TryParse(preco, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pr)) pr = 59.90m;
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        using var conn = _db.Connect(); conn.Open();
        conn.Execute("INSERT INTO produtos (id,nome,categoria,ativo,max_sabores,created_at,updated_at) VALUES (@id,@nome,@cat,1,2,@now,@now)", new { id, nome, cat, now });
        var varId = Guid.NewGuid().ToString();
        conn.Execute("INSERT INTO variacoes (id,produto_id,tamanho,preco,custo,ativo,updated_at) VALUES (@id,@pid,'G',@preco,0,1,@now)", new { id = varId, pid = id, preco = pr, now });
        _sync.Enqueue("produtos", "insert", new { id, nome, categoria = cat, ativo = true, max_sabores = 2, updated_at = now });
        _sync.Enqueue("variacoes", "insert", new { id = varId, produto_id = id, tamanho = "G", preco = pr });
        MessageBox.Show("Produto criado. Clique em Sincronizar para ir pro Site.");
        LoadCardapio();
    }

    private void EditarPreco(DataGridView dgv)
    {
        if (dgv.SelectedRows.Count == 0) { MessageBox.Show("Selecione um produto"); return; }
        var nome = dgv.SelectedRows[0].Cells[0].Value?.ToString();
        var novo = Prompt($"Novo preço para {nome}:", "59.90");
        if (!decimal.TryParse(novo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pr)) return;
        using var conn = _db.Connect(); conn.Open();
        var pid = conn.ExecuteScalar<string>("SELECT id FROM produtos WHERE nome=@n", new { n = nome });
        if (pid != null)
        {
            conn.Execute("UPDATE variacoes SET preco=@p, updated_at=@now WHERE produto_id=@pid", new { p = pr, now = DateTime.UtcNow.ToString("o"), pid });
            _sync.Enqueue("variacoes", "update", new { produto_id = pid, preco = pr });
            MessageBox.Show("Preço atualizado. Sincronize.");
            LoadCardapio();
        }
    }

    private void ToggleProduto(DataGridView dgv) => MessageBox.Show("Desativar: UPDATE produtos SET ativo=0 (implementar)");
    private void EditarBorda(DataGridView dgv) => MessageBox.Show("Editar borda: UPDATE bordas SET preco_adicional (implementar inline)");
    private void NovoBairro()
    {
        var nome = Prompt("Nome do bairro:", "Centro");
        var taxa = Prompt("Taxa (ex: 5.00):", "5.00");
        if (!decimal.TryParse(taxa, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t)) return;
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        using var conn2 = _db.Connect(); conn2.Open();
        conn2.Execute("INSERT INTO bairros (id,nome,taxa_fixa,ativo,created_at,updated_at) VALUES (@id,@n,@t,1,@now,@now)", new { id, n = nome, t, now });
        _sync.Enqueue("bairros", "insert", new { id, nome, taxa_fixa = t });
        LoadCardapio();
    }
    private void EditarBairro(DataGridView dgv) => MessageBox.Show("Editar taxa: UPDATE bairros SET taxa_fixa");
    private void NovoSabor()
    {
        var nome = Prompt("Nome do sabor:", "Calabresa");
        if (string.IsNullOrWhiteSpace(nome)) return;
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        using var conn3 = _db.Connect(); conn3.Open();
        conn3.Execute("INSERT INTO sabores (id,nome,categoria,custo,ativo,updated_at) VALUES (@id,@n,'salgada',0,1,@now)", new { id, n = nome, now });
        _sync.Enqueue("sabores", "insert", new { id, nome });
        LoadCardapio();
    }

    // === Caixa ===
    private void LoadCaixa()
    {
        pnlMain.Controls.Clear();
        var title = new Label { Text = "CAIXA — F4 | Saldo inicial, sangria, fechamento", Dock = DockStyle.Top, Height = 24, Font = new Font("Consolas", 10, FontStyle.Bold) };
        pnlMain.Controls.Add(title);
        using var conn = _db.Connect(); conn.Open();
        var caixa = conn.QueryFirstOrDefault("SELECT saldo_inicial, total_vendas FROM caixa_local ORDER BY aberto_em DESC LIMIT 1");
        var pnl = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 3 };
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        pnl.Controls.Add(MakeKpi("Saldo Inicial", caixa != null ? $"R$ {Convert.ToDecimal(caixa.saldo_inicial):F2}" : "R$ 100,00"));
        pnl.Controls.Add(MakeKpi("Total Vendas Hoje", caixa != null ? $"R$ {Convert.ToDecimal(caixa.total_vendas):F2}" : "R$ 1.070,00"));
        pnl.Controls.Add(MakeKpi("Comissão Entregador", "R$ 84,00 (12 x taxa)"));
        pnlMain.Controls.Add(pnl);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
        bar.Controls.Add(MakeAction("Sangria", () => MessageBox.Show("Sangria: INSERT caixa_local.sangrias")));
        bar.Controls.Add(MakeAction("Suprimento", () => MessageBox.Show("Suprimento")));
        bar.Controls.Add(MakeAction("Fechar Caixa", () => MessageBox.Show("Fechar: UPDATE caixa_local SET fechado_em"), Vermelho, Color.White));
        bar.Controls.Add(MakeAction("Imprimir Fechamento", () => TestPrint(), Amarelo, Color.Black));
        pnlMain.Controls.Add(bar);
        var dgv = new DataGridView { Dock = DockStyle.Fill, DataSource = new[] { new { Forma = "Dinheiro", Valor = "R$ 320,00" }, new { Forma = "Pix", Valor = "R$ 540,00" }, new { Forma = "Cartão InfinitePay", Valor = "R$ 210,00" } }.ToList(), Font = new Font("Consolas", 9) };
        pnlMain.Controls.Add(dgv);
    }

    private void LoadClientes()
    {
        pnlMain.Controls.Clear();
        var title = new Label { Text = "CLIENTES & FIDELIDADE — F5 | 10 G → 1 P gratis", Dock = DockStyle.Top, Height = 24, Font = new Font("Consolas", 10, FontStyle.Bold) };
        pnlMain.Controls.Add(title);
        var dgv = new DataGridView { Dock = DockStyle.Fill, DataSource = new[] { new { Nome = "João Silva", Telefone = "88999990000", G = "7/10", Cupons = 0 }, new { Nome = "Maria", Telefone = "88988880000", G = "10/10", Cupons = 1 } }.ToList(), Font = new Font("Consolas", 9) };
        pnlMain.Controls.Add(dgv);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
        var txt = new TextBox { Width = 180, PlaceholderText = "Buscar telefone" };
        bar.Controls.Add(txt);
        bar.Controls.Add(MakeAction("Buscar", () => MessageBox.Show("Buscar cliente")));
        bar.Controls.Add(MakeAction("Resgatar Pizza P", () => MessageBox.Show("Cupom resgatado!"), Vermelho, Color.White));
        pnlMain.Controls.Add(bar);
    }

    // Helpers UI
    private Control MakeKpi(string title, string value)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(6), BorderStyle = BorderStyle.FixedSingle };
        p.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 20, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Consolas", 8), ForeColor = Color.Gray });
        p.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Consolas", 14, FontStyle.Bold) });
        return p;
    }

    private Button MakeAction(string text, Action onClick, Color? back = null, Color? fore = null)
    {
        var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = back ?? Color.FromArgb(45,45,45), ForeColor = fore ?? Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Padding = new Padding(8,4,8,4), Margin = new Padding(4) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, __) => onClick();
        return b;
    }

    private void TestPrint()
    {
        var p = new PedidoPrint("teste1234", "balcao", "Teste Batata", "88999990000", "Rua Teste 123 - Centro", "Centro", 5, null, null,
            new List<ItemPrint> { new ItemPrint("Pizza G Mussarela", 1, 59.90m, "Borda Catupiry", "sem cebola"), new ItemPrint("Refrigerante", 1, 5m, null, null) },
            64.90m, 69.90m, "dinheiro", DateTime.Now.ToString("HH:mm"), "batata teste");
        var raw = Templates.TicketCliente(p);
        var (ok, via) = RawPrinter.PrintAuto(raw);
        MessageBox.Show(ok ? $"Teste impresso via {via}\n\n{raw}" : $"{via}\n\n{raw}", "Teste JP-58H", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private string? Prompt(string text, string def)
    {
        var f = new Form { Width = 380, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog };
        var tb = new TextBox { Text = def, Dock = DockStyle.Top, Margin = new Padding(12) };
        f.Controls.Add(tb);
        var btn = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 30 };
        f.Controls.Add(btn);
        f.AcceptButton = btn;
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }
}
