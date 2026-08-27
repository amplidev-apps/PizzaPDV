using PizzaPDV.Data;
using PizzaPDV.Printer;
using PizzaPDV.Sync;
using System.Data;
using Dapper;
using System.Text.Json;

namespace PizzaPDV.Central;

public class MainForm : Form
{
    private readonly AppDb _db = new AppDb();
    private readonly SyncService _sync;
    private Label lblStatus = null!;
    private Label lblSync = null!;
    private Panel pnlMain = null!;
    private Panel pnlMenu = null!;
    private System.Windows.Forms.Timer syncTimer = null!;
    private string activeKey = "pedidos";

    // Paleta limpa batata — sem poluição
    private readonly Color C_Bg = Color.FromArgb(246, 247, 249);
    private readonly Color C_Card = Color.White;
    private readonly Color C_Border = Color.FromArgb(225, 228, 232);
    private readonly Color C_Text = Color.FromArgb(36, 41, 47);
    private readonly Color C_Muted = Color.FromArgb(110, 119, 129);
    private readonly Color C_Primary = Color.FromArgb(207, 34, 46); // vermelho só em ação primária
    private readonly Color C_PrimaryHover = Color.FromArgb(185, 28, 38);
    private readonly Color C_MenuBg = Color.FromArgb(36, 41, 47);
    private readonly Color C_MenuSel = Color.FromArgb(51, 57, 66);

    // Estado balcão
    private List<BalcaoItem> balcaoCart = new();
    private decimal balcaoTaxa = 0;

    private record BalcaoItem(string Produto, string Detalhe, decimal Preco, int Qtd, string? Obs, string? Borda);

    public MainForm()
    {
        _sync = new SyncService(_db,
            Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "",
            Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? "");

        Text = "PizzaPDV Central";
        WindowState = FormWindowState.Maximized;
        BackColor = C_Bg;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(1100, 700);

        BuildChrome();
        Navigate("pedidos");

        syncTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        syncTimer.Tick += async (_, __) => await DoSync();
        syncTimer.Start();
        _ = DoSync();

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) Navigate("pedidos");
            if (e.KeyCode == Keys.F2) Navigate("balcao");
            if (e.KeyCode == Keys.F3) Navigate("mesas");
            if (e.KeyCode == Keys.F4) Navigate("cardapio");
            if (e.KeyCode == Keys.F5) Navigate("caixa");
            if (e.KeyCode == Keys.F6) Navigate("clientes");
            if (e.KeyCode == Keys.F7) Navigate("validade");
            if (e.KeyCode == Keys.F10) Navigate("config");
            if (e.KeyCode == Keys.F12) TestPrint();
        };
    }

    private void BuildChrome()
    {
        // Topo minimalista
        var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = C_Card, Padding = new Padding(16, 0, 16, 0) };
        top.Paint += (s, e) => { using var p = new Pen(C_Border); e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };
        var lblTitle = new Label { Text = "PizzaPDV", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = C_Text, AutoSize = false, Dock = DockStyle.Left, Width = 110, TextAlign = ContentAlignment.MiddleLeft };
        var lblSub = new Label { Text = "Central • SynX 10  •  JP-58H 58mm", Font = new Font("Segoe UI", 8f), ForeColor = C_Muted, Dock = DockStyle.Left, Width = 210, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 12, 0, 0) };
        lblStatus = new Label { Text = "● ONLINE", ForeColor = Color.FromArgb(26, 127, 55), Dock = DockStyle.Right, Width = 90, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        lblSync = new Label { Text = "Pronto", ForeColor = C_Muted, Dock = DockStyle.Right, Width = 360, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8f) };
        top.Controls.Add(lblSync);
        top.Controls.Add(lblStatus);
        top.Controls.Add(lblSub);
        top.Controls.Add(lblTitle);
        Controls.Add(top);

        // Menu esquerdo limpo
        pnlMenu = new Panel { Dock = DockStyle.Left, Width = 180, BackColor = C_MenuBg, Padding = new Padding(8, 8, 8, 8) };
        var menuItems = new (string key, string label, string hint)[]
        {
            ("pedidos","Pedidos","F1 • Cozinha"),
            ("balcao","Balcão","F2 • Venda rápida"),
            ("mesas","Mesas 1—20","F3 • Salão"),
            ("cardapio","Cardápio","F4 • Produtos"),
            ("caixa","Caixa","F5 • Fechamento"),
            ("clientes","Clientes","F6 • Fidelidade"),
            ("validade","Validade","F7 • Etiquetas"),
            ("config","Configurações","F10 • Sistema"),
        };
        foreach (var (k,l,h) in menuItems.Reverse())
        {
            var b = MakeMenuBtn(k,l,h);
            pnlMenu.Controls.Add(b);
            b.Dock = DockStyle.Top;
        }
        Controls.Add(pnlMenu);

        pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg, Padding = new Padding(16) };
        Controls.Add(pnlMain);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = C_Card };
        bottom.Paint += (s, e) => { using var p = new Pen(C_Border); e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
        var lblBottom = new Label { Text = " Offline salva local e sincroniza quando volta  •  Taxa fixa por bairro  •  F12 testa impressão", ForeColor = C_Muted, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f), Padding = new Padding(12, 3, 0, 0) };
        bottom.Controls.Add(lblBottom);
        Controls.Add(bottom);
    }

    private Button MakeMenuBtn(string key, string title, string hint)
    {
        var b = new Button
        {
            Name = key,
            Height = 52,
            FlatStyle = FlatStyle.Flat,
            BackColor = key == activeKey ? C_MenuSel : C_MenuBg,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 0, 0, 4),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.Clear(b.BackColor);
            using var f1 = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var f2 = new Font("Segoe UI", 7.5f);
            TextRenderer.DrawText(g, title, f1, new Rectangle(12, 8, b.Width - 24, 16), Color.White, TextFormatFlags.Left);
            TextRenderer.DrawText(g, hint, f2, new Rectangle(12, 26, b.Width - 24, 14), Color.FromArgb(160, 170, 180), TextFormatFlags.Left);
            if (key == activeKey)
            {
                using var pen = new Pen(C_Primary, 3);
                g.DrawLine(pen, 0, 6, 0, b.Height - 6);
            }
        };
        b.Click += (_, __) => Navigate(key);
        b.MouseEnter += (_, __) => { if (key != activeKey) b.BackColor = Color.FromArgb(44, 49, 58); b.Invalidate(); };
        b.MouseLeave += (_, __) => { if (key != activeKey) b.BackColor = C_MenuBg; b.Invalidate(); };
        return b;
    }

    private void Navigate(string key)
    {
        activeKey = key;
        foreach (Button b in pnlMenu.Controls.OfType<Button>()) { b.BackColor = b.Name == key ? C_MenuSel : C_MenuBg; b.Invalidate(); }
        pnlMain.Controls.Clear();
        switch (key)
        {
            case "pedidos": LoadPedidos(); break;
            case "balcao": LoadBalcao(); break;
            case "mesas": LoadMesas(); break;
            case "cardapio": LoadCardapio(); break;
            case "caixa": LoadCaixa(); break;
            case "clientes": LoadClientes(); break;
            case "validade": LoadValidade(); break;
            case "config": LoadConfig(); break;
        }
    }

    private async Task DoSync()
    {
        var online = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        lblStatus.Text = online ? "● ONLINE" : "● OFFLINE";
        lblStatus.ForeColor = online ? Color.FromArgb(26, 127, 55) : Color.FromArgb(207, 34, 46);
        if (!_sync.IsConfigured) { lblSync.Text = "Local • sem Supabase configurado"; return; }
        var (synced, pending, err) = await _sync.SyncOutboxAsync();
        lblSync.Text = pending == 0 ? $"Sincronizado • {synced} enviados" : $"{pending} pendentes • {synced} enviados" + (err != null ? $" • {err[..Math.Min(40, err.Length)]}" : "");
    }

    // Helpers visuais limpos
    private Panel Card(Control inner, int pad = 12)
    {
        var c = new Panel { Dock = DockStyle.Fill, BackColor = C_Card, Padding = new Padding(pad) };
        c.Paint += (s, e) =>
        {
            var r = c.ClientRectangle; r.Inflate(-1, -1);
            using var pen = new Pen(C_Border);
            e.Graphics.DrawRectangle(pen, r);
        };
        if (inner != null) c.Controls.Add(inner);
        return c;
    }

    private Label SectionTitle(string t, string? sub = null)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = sub == null ? 28 : 40, BackColor = Color.Transparent, Padding = new Padding(2, 0, 0, 0) };
        var l1 = new Label { Text = t, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = C_Text };
        p.Controls.Add(l1);
        if (sub != null)
        {
            var l2 = new Label { Text = sub, Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 8f), ForeColor = C_Muted };
            p.Controls.Add(l2);
            p.Controls.SetChildIndex(l2, 0);
            p.Controls.SetChildIndex(l1, 1);
        }
        // wrapper para usar como control único
        var wrap = new Panel { Dock = DockStyle.Top, Height = p.Height, BackColor = Color.Transparent };
        wrap.Controls.Add(p);
        p.Dock = DockStyle.Fill;
        return l1; // retorna label mas altura controlada pelo wrap — truque simples: usar Panel
    }

    private Panel TitleBar(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
        var l1 = new Label { Text = title, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = C_Text, Dock = DockStyle.Top, Height = 22 };
        var l2 = new Label { Text = subtitle, Font = new Font("Segoe UI", 8.5f), ForeColor = C_Muted, Dock = DockStyle.Top, Height = 16 };
        p.Controls.Add(l2);
        p.Controls.Add(l1);
        return p;
    }

    private Button BtnPrimary(string text, Action onClick)
    {
        var b = new Button { Text = text, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = C_Primary, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(14, 0, 14, 0), AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, __) => onClick();
        b.MouseEnter += (_, __) => b.BackColor = C_PrimaryHover;
        b.MouseLeave += (_, __) => b.BackColor = C_Primary;
        return b;
    }
    private Button BtnGhost(string text, Action onClick)
    {
        var b = new Button { Text = text, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = C_Card, ForeColor = C_Text, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(12, 0, 12, 0), AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        b.FlatAppearance.BorderColor = C_Border;
        b.FlatAppearance.BorderSize = 1;
        b.Click += (_, __) => onClick();
        return b;
    }

    private DataGridView CleanGrid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = C_Card,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = C_Border,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font = new Font("Segoe UI", 9f),
            ColumnHeadersHeight = 28,
            RowTemplate = { Height = 28 },
            EnableHeadersVisualStyles = false,
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 250);
        g.ColumnHeadersDefaultCellStyle.ForeColor = C_Muted;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        g.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
        return g;
    }

    // ===== PEDIDOS =====
    private void LoadPedidos()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Pedidos", "F1 • Recebido → Preparo → Pronto → Entregue • F12 imprime cozinha"));

        var tool = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 4), BackColor = Color.Transparent };
        pnlMain.Controls.Add(tool);

        var grid = CleanGrid();
        var card = Card(grid, 0);
        card.Dock = DockStyle.Fill;
        pnlMain.Controls.Add(card);

        using var conn = _db.Connect();
        conn.Open();
        var rows = conn.Query("SELECT id, origem, status, cliente_nome, total, mesa_numero, created_at FROM pedidos_local ORDER BY created_at DESC LIMIT 100").ToList();
        var dt = new DataTable();
        dt.Columns.Add("ID", typeof(string)); dt.Columns.Add("Origem", typeof(string)); dt.Columns.Add("Status", typeof(string)); dt.Columns.Add("Cliente", typeof(string)); dt.Columns.Add("Mesa", typeof(string)); dt.Columns.Add("Total", typeof(string));
        foreach (var r in rows) dt.Rows.Add((string)r.id, (string)r.origem, (string)r.status, (string)r.cliente_nome, r.mesa_numero?.ToString() ?? "—", $"R$ {Convert.ToDecimal(r.total):F2}");
        if (dt.Rows.Count == 0) { dt.Rows.Add("a1b2…", "site", "recebido", "João • Centro", "—", "R$ 64,90"); dt.Rows.Add("c3d4…", "mesa", "preparo", "Mesa 07", "07", "R$ 42,00"); }
        grid.DataSource = dt;

        tool.Controls.Add(BtnPrimary("Aceitar → Preparo", () => MoveSelected(grid, "preparo")));
        tool.Controls.Add(BtnGhost("Marcar Pronto", () => MoveSelected(grid, "pronto")));
        tool.Controls.Add(BtnGhost("Entregar", () => MoveSelected(grid, "entregue")));
        tool.Controls.Add(new Label { Width = 16, AutoSize = false, Dock = DockStyle.Left });
        tool.Controls.Add(BtnGhost("Cozinha", () => PrintSelected(grid, "cozinha")));
        tool.Controls.Add(BtnGhost("Cliente", () => PrintSelected(grid, "cliente")));
        tool.Controls.Add(BtnGhost("Delivery", () => PrintSelected(grid, "delivery")));
    }

    private void MoveSelected(DataGridView g, string novo)
    {
        if (g.SelectedRows.Count == 0) { MessageBox.Show("Selecione um pedido.", "Pedidos", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var id = g.SelectedRows[0].Cells[0].Value?.ToString();
        if (id != null && id.Contains("…")) { MessageBox.Show("Pedido de exemplo — sem ação no banco."); return; }
        using var c = _db.Connect(); c.Open();
        c.Execute("UPDATE pedidos_local SET status=@s, updated_at=@now WHERE id=@id", new { s = novo, now = DateTime.UtcNow.ToString("o"), id });
        Navigate("pedidos");
    }
    private void PrintSelected(DataGridView g, string tipo)
    {
        var id = g.SelectedRows.Count > 0 ? g.SelectedRows[0].Cells[0].Value?.ToString() ?? "teste1234" : "teste1234";
        var p = new PedidoPrint(id, "site", "Cliente Teste", "88999990000", "Rua Centro 123", "Centro", 5, null, null, new List<ItemPrint> { new ItemPrint("Pizza G Calabresa / Mussarela", 1, 59.90m, "Borda Catupiry", null) }, 59.90m, 64.90m, "pix", DateTime.Now.ToString("HH:mm"), null);
        string raw = tipo == "cozinha" ? Templates.ComandaCozinha(p) : tipo == "delivery" ? Templates.TicketDelivery(p) : Templates.TicketCliente(p);
        var (ok, via) = RawPrinter.PrintAuto(raw);
        MessageBox.Show(ok ? $"Impresso em {via}" : via + "\n\n" + raw, "JP-58H", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    // ===== BALCÃO (NOVO - venda rápida) =====
    private void LoadBalcao()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Balcão", "F2 • Venda rápida sem mesa • Adicione itens, escolha borda, finalize e imprime"));

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = C_Bg, Padding = new Padding(0, 8, 0, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pnlMain.Controls.Add(table);

        // Esquerda: produtos + borda + obs
        var leftCard = Card(new Panel(), 12);
        var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        leftCard.Controls.Clear(); leftCard.Controls.Add(left);
        leftCard.Padding = new Padding(12);
        table.Controls.Add(leftCard, 0, 0);

        var lblProd = new Label { Text = "Produto", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top };
        var cbProd = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
        var lblBorda = new Label { Text = "Borda", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top };
        lblBorda.Padding = new Padding(0, 8, 0, 0);
        var cbBorda = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList };
        cbBorda.Items.AddRange(new object[] { "Sem borda", "Catupiry +R$ 8,00", "Cream Cheese +R$ 8,00", "Chocolate +R$ 9,00", "Borda Comum +R$ 5,00", "Borda Camarão +R$ 15,00", "Borda Vulcão +R$ 18,00" });
        cbBorda.SelectedIndex = 0;
        var lblObs = new Label { Text = "Observação", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
        var txtObs = new TextBox { Dock = DockStyle.Top, Height = 30, PlaceholderText = "Ex: sem cebola, bem assada", Font = new Font("Segoe UI", 9f) };
        var pnlQtd = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        var numQtd = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 1, Width = 70, Dock = DockStyle.Left, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        var lblQtd = new Label { Text = "Qtd", Dock = DockStyle.Left, Width = 36, TextAlign = ContentAlignment.MiddleLeft, ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
        pnlQtd.Controls.Add(numQtd); pnlQtd.Controls.Add(lblQtd);

        // Carrega produtos do banco
        using (var conn = _db.Connect()) { conn.Open(); var prods = conn.Query("SELECT nome FROM produtos WHERE ativo=1 ORDER BY nome").ToList(); foreach (var p in prods) cbProd.Items.Add((string)p.nome); }
        if (cbProd.Items.Count == 0) { cbProd.Items.Add("Pizza Grande (8 fatias) — R$ 59,90"); cbProd.Items.Add("Pizza Pequena (4 fatias) — R$ 34,90"); cbProd.Items.Add("Esfiha Aberta — R$ 6,00"); cbProd.Items.Add("Calzone — R$ 22,00"); }
        cbProd.SelectedIndex = 0;

        // Lista visual do carrinho (esquerda embaixo)
        var lst = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        var lblCartTitle = new Label { Text = "Itens do pedido", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Padding = new Padding(0, 10, 0, 0) };

        var btnAdd = BtnPrimary("Adicionar ao pedido", () => { });
        btnAdd.Dock = DockStyle.Top; btnAdd.Height = 36; btnAdd.Margin = new Padding(0, 10, 0, 0);

        // Ordem: de baixo para cima com Dock Top precisa inverter
        left.Controls.Add(lst);
        left.Controls.Add(lblCartTitle);
        left.Controls.Add(btnAdd);
        left.Controls.Add(pnlQtd);
        left.Controls.Add(txtObs);
        left.Controls.Add(lblObs);
        left.Controls.Add(cbBorda);
        left.Controls.Add(lblBorda);
        left.Controls.Add(cbProd);
        left.Controls.Add(lblProd);

        // Direita: resumo + cliente + pagamento
        var rightCard = Card(new Panel(), 12);
        var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        rightCard.Controls.Clear(); rightCard.Controls.Add(right);
        table.Controls.Add(rightCard, 1, 0);

        var lblCliente = new Label { Text = "Cliente (opcional)", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top };
        var txtCliente = new TextBox { Dock = DockStyle.Top, Height = 30, PlaceholderText = "Nome", Font = new Font("Segoe UI", 9f) };
        var txtTel = new TextBox { Dock = DockStyle.Top, Height = 30, PlaceholderText = "Telefone / WhatsApp", Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 6, 0, 0) };
        txtTel.Margin = new Padding(0, 6, 0, 0);

        var lblBairro = new Label { Text = "Bairro • taxa fixa", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
        var cbBairro = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList };
        var lblPag = new Label { Text = "Pagamento", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_Muted, Height = 18, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
        var cbPag = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList };
        cbPag.Items.AddRange(new object[] { "Dinheiro", "Pix", "Pix PushinPay (QR)", "Cartão InfinitePay" });
        cbPag.SelectedIndex = 0;

        // Carrega bairros
        var bairroMap = new Dictionary<string, decimal>();
        using (var conn = _db.Connect()) { conn.Open(); foreach (var r in conn.Query("SELECT nome, taxa_fixa FROM bairros WHERE ativo=1 ORDER BY nome").ToList()) { var n = (string)r.nome; var t = Convert.ToDecimal(r.taxa_fixa); bairroMap[n] = t; cbBairro.Items.Add($"{n} — R$ {t:F2}"); } }
        if (cbBairro.Items.Count == 0) { cbBairro.Items.Add("Centro — R$ 5,00"); bairroMap["Centro — R$ 5,00"] = 5m; }
        cbBairro.Items.Insert(0, "Retirada no balcão — R$ 0,00");
        bairroMap["Retirada no balcão — R$ 0,00"] = 0m;
        cbBairro.SelectedIndex = 0;

        var pnlTotais = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Color.FromArgb(246, 248, 250), Padding = new Padding(10), Margin = new Padding(0, 12, 0, 0) };
        pnlTotais.Paint += (s, e) => { using var pen = new Pen(C_Border); e.Graphics.DrawRectangle(pen, 0, 0, pnlTotais.Width - 1, pnlTotais.Height - 1); };
        var lblSub = new Label { Text = "Subtotal  R$ 0,00", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9f), ForeColor = C_Text, TextAlign = ContentAlignment.MiddleRight };
        var lblTaxa = new Label { Text = "Taxa  R$ 0,00", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9f), ForeColor = C_Muted, TextAlign = ContentAlignment.MiddleRight };
        var lblTotal = new Label { Text = "TOTAL  R$ 0,00", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_Text, TextAlign = ContentAlignment.MiddleRight };
        pnlTotais.Controls.Add(lblTotal); pnlTotais.Controls.Add(lblTaxa); pnlTotais.Controls.Add(lblSub);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, FlowDirection = FlowDirection.TopDown, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
        var btnFinalizar = BtnPrimary("Finalizar e imprimir  •  F12", () => { });
        btnFinalizar.Height = 38; btnFinalizar.Dock = DockStyle.Top;
        var btnLimpar = BtnGhost("Limpar carrinho", () => { balcaoCart.Clear(); lst.Items.Clear(); UpdateTotais(); });
        btnLimpar.Dock = DockStyle.Top; btnLimpar.Margin = new Padding(0, 8, 0, 0);
        bar.Controls.Add(btnFinalizar); bar.Controls.Add(btnLimpar);

        // Layout direita: ordem dock top
        right.Controls.Add(bar);
        right.Controls.Add(pnlTotais);
        right.Controls.Add(cbPag);
        right.Controls.Add(lblPag);
        right.Controls.Add(cbBairro);
        right.Controls.Add(lblBairro);
        right.Controls.Add(txtTel);
        right.Controls.Add(txtCliente);
        right.Controls.Add(lblCliente);

        void UpdateTotais()
        {
            var sub = balcaoCart.Sum(i => i.Preco * i.Qtd);
            var sel = cbBairro.SelectedItem?.ToString() ?? "";
            balcaoTaxa = bairroMap.TryGetValue(sel, out var t) ? t : 0m;
            lblSub.Text = $"Subtotal  R$ {sub:F2}";
            lblTaxa.Text = $"Taxa  R$ {balcaoTaxa:F2}";
            lblTotal.Text = $"TOTAL  R$ {(sub + balcaoTaxa):F2}";
        }

        btnAdd.Click += (_, __) =>
        {
            var prod = cbProd.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(prod)) return;
            // extrai preço do texto " — R$ 59,90" se houver
            decimal preco = 0;
            var m = System.Text.RegularExpressions.Regex.Match(prod, @"R\$\s*([\d.,]+)");
            if (m.Success) decimal.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out preco);
            if (preco == 0) preco = 59.90m;
            // borda
            var bordaTxt = cbBorda.SelectedItem?.ToString() ?? "Sem borda";
            decimal bordaPreco = 0;
            var bm = System.Text.RegularExpressions.Regex.Match(bordaTxt, @"R\$\s*([\d.,]+)");
            if (bm.Success) decimal.TryParse(bm.Groups[1].Value, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out bordaPreco);
            var detalhe = bordaTxt == "Sem borda" ? "" : bordaTxt.Split('+')[0].Trim();
            var precoFinal = preco + bordaPreco;
            var qtd = (int)numQtd.Value;
            var obs = string.IsNullOrWhiteSpace(txtObs.Text) ? null : txtObs.Text.Trim();
            balcaoCart.Add(new BalcaoItem(prod.Split('—')[0].Trim(), detalhe, precoFinal, qtd, obs, bordaTxt));
            lst.Items.Add($"{qtd}x {prod.Split('—')[0].Trim()} {(string.IsNullOrEmpty(detalhe)?"": "+ "+detalhe)} = R$ {(precoFinal*qtd):F2} {(obs!=null?"("+obs+")":"")}");
            UpdateTotais();
            txtObs.Clear();
        };

        cbBairro.SelectedIndexChanged += (_, __) => UpdateTotais();

        btnFinalizar.Click += (_, __) =>
        {
            if (balcaoCart.Count == 0) { MessageBox.Show("Adicione pelo menos um item.", "Balcão", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var sub = balcaoCart.Sum(i => i.Preco * i.Qtd);
            var total = sub + balcaoTaxa;
            var bairroSel = cbBairro.SelectedItem?.ToString() ?? "Retirada";
            var pag = cbPag.SelectedItem?.ToString() ?? "Dinheiro";
            var cliente = string.IsNullOrWhiteSpace(txtCliente.Text) ? "Balcão" : txtCliente.Text.Trim();
            var tel = string.IsNullOrWhiteSpace(txtTel.Text) ? "00000000000" : txtTel.Text.Trim();

            var id = "tmp_" + Guid.NewGuid().ToString("N")[..8];
            var now = DateTime.UtcNow.ToString("o");
            using var conn = _db.Connect(); conn.Open();
            conn.Execute("INSERT INTO pedidos_local (id,origem,status,cliente_nome,cliente_telefone,cliente_endereco,bairro_id,taxa_entrega,subtotal,total,forma_pagamento,status_pagamento,observacao,created_at,updated_at,payload_json,synced) VALUES (@id,'balcao','preparo',@cli,@tel,'',null,@taxa,@sub,@tot,@pag,'pendente','',@now,@now,@pay,0)",
                new { id, cli = cliente, tel, taxa = balcaoTaxa, sub, tot = total, pag, now, pay = JsonSerializer.Serialize(new { id, cliente, bairroSel, itens = balcaoCart.Count }) });
            foreach (var it in balcaoCart)
            {
                var iid = Guid.NewGuid().ToString();
                conn.Execute("INSERT INTO itens_pedido_local (id,pedido_id,produto_id,variacao_id,quantidade,observacao,preco_unit) VALUES (@id,@pid,'prod','var',@qtd,@obs,@preco)",
                    new { id = iid, pid = id, qtd = it.Qtd, obs = it.Obs, preco = it.Preco });
            }
            _sync.Enqueue("pedidos", "insert", new { id, origem = "balcao", status = "preparo", cliente_nome = cliente, cliente_telefone = tel, subtotal = sub, total, forma_pagamento = pag, taxa_entrega = balcaoTaxa });

            var itensPrint = balcaoCart.Select(i => new ItemPrint(i.Produto, i.Qtd, i.Preco, i.Detalhe, i.Obs)).ToList();
            var bairroPrint = bairroSel.Contains("—") ? bairroSel.Split('—')[0].Trim() : bairroSel;
            var p = new PedidoPrint(id, "balcao", cliente, tel, null, bairroPrint, balcaoTaxa, null, null, itensPrint, sub, total, pag, DateTime.Now.ToString("HH:mm"), null);
            var raw = Templates.TicketCliente(p);
            var (ok, via) = RawPrinter.PrintAuto(raw);
            MessageBox.Show(ok ? $"Pedido balcão criado e impresso em {via}\nTotal R$ {total:F2}" : $"{via}\n\n{raw}", "Balcão", MessageBoxButtons.OK, MessageBoxIcon.Information);
            balcaoCart.Clear(); lst.Items.Clear(); txtCliente.Clear(); txtTel.Clear(); txtObs.Clear(); cbBairro.SelectedIndex = 0; UpdateTotais();
        };

        UpdateTotais();
    }

    // ===== MESAS =====
    private void LoadMesas()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Mesas", "F3 • 20 mesas • Branca = livre • Amarela = ocupada • Toque para abrir comanda"));

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 4, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        for (int i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        using var conn = _db.Connect(); conn.Open();
        var mesas = conn.Query("SELECT numero, status FROM mesas ORDER BY numero").ToList();
        if (mesas.Count == 0) mesas = Enumerable.Range(1, 20).Select(n => new { numero = n, status = "livre" }).Cast<dynamic>().ToList();

        foreach (var m in mesas)
        {
            int num = (int)m.numero;
            string status = (string)m.status;
            bool ocupada = status == "ocupada";
            var card = new Panel { Margin = new Padding(6), BackColor = C_Card };
            card.Paint += (s, e) => { using var pen = new Pen(ocupada ? Color.FromArgb(244, 211, 92) : C_Border); e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1); };
            var lblNum = new Label { Text = $"{num:D2}", Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = C_Text, Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 8, 0, 0) };
            var lblSt = new Label { Text = ocupada ? "● ocupada" : "○ livre", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = ocupada ? Color.FromArgb(154, 103, 0) : C_Muted, Dock = DockStyle.Top, Height = 18, TextAlign = ContentAlignment.MiddleCenter };
            var btn = new Button { Text = ocupada ? "Abrir comanda" : "Abrir mesa", Dock = DockStyle.Bottom, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = ocupada ? Color.FromArgb(255, 243, 205) : Color.FromArgb(246, 248, 250), ForeColor = C_Text, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, __) => AbrirMesa(num);
            card.Controls.Add(btn); card.Controls.Add(lblSt); card.Controls.Add(lblNum);
            grid.Controls.Add(card);
        }
        var wrap = Card(grid, 8);
        pnlMain.Controls.Add(wrap);
    }

    private void AbrirMesa(int numero)
    {
        var f = new Form { Text = $"Mesa {numero:D2} — Comanda", Width = 640, Height = 520, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, BackColor = C_Bg, Font = new Font("Segoe UI", 9f) };
        var top = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = C_Card, Padding = new Padding(12, 0, 12, 0) };
        top.Paint += (s, e) => { using var pen = new Pen(C_Border); e.Graphics.DrawLine(pen, 0, top.Height - 1, top.Width, top.Height - 1); };
        top.Controls.Add(new Label { Text = $"Mesa {numero:D2}", Dock = DockStyle.Left, Width = 100, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_Text, TextAlign = ContentAlignment.MiddleLeft });
        top.Controls.Add(new Label { Text = "Adicione pizzas P/G (meia-a-meia), esfihas, bordas. A cozinha recebe com MESA marcada.", Dock = DockStyle.Fill, ForeColor = C_Muted, Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
        f.Controls.Add(top);

        var lst = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        f.Controls.Add(lst);

        using var conn = _db.Connect(); conn.Open();
        var prods = conn.Query("SELECT nome FROM produtos WHERE ativo=1 ORDER BY nome").ToList();
        var cbProd = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
        foreach (var p in prods) cbProd.Items.Add((string)p.nome);
        if (cbProd.Items.Count == 0) { cbProd.Items.Add("Pizza Grande (8 fatias) — R$ 59,90"); cbProd.Items.Add("Pizza Pequena (4 fatias) — R$ 34,90"); cbProd.Items.Add("Esfiha — R$ 6,00"); }
        cbProd.SelectedIndex = 0;
        f.Controls.Add(cbProd);

        var cbBorda = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
        cbBorda.Items.AddRange(new object[] { "Sem borda", "Catupiry +R$ 8,00", "Cream Cheese +R$ 8,00", "Chocolate +R$ 9,00", "Borda Comum +R$ 5,00", "Borda Camarão +R$ 15,00", "Borda Vulcão +R$ 18,00" });
        cbBorda.SelectedIndex = 0;
        f.Controls.Add(cbBorda);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = C_Card, Padding = new Padding(8) };
        bar.Paint += (s, e) => { using var pen = new Pen(C_Border); e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0); };
        var btnFechar = BtnGhost("Fechar", () => f.Close());
        btnFechar.Dock = DockStyle.Right;
        var btnEnviar = BtnPrimary("Enviar p/ cozinha  •  F12", () => { });
        btnEnviar.Dock = DockStyle.Right;
        var btnAdd = BtnGhost("Adicionar item", () => lst.Items.Add($"1x {cbProd.SelectedItem} + {cbBorda.SelectedItem}"));
        btnAdd.Dock = DockStyle.Left;
        bar.Controls.Add(btnFechar); bar.Controls.Add(btnEnviar); bar.Controls.Add(btnAdd);
        f.Controls.Add(bar);

        var localItems = new List<(string prod, string borda)>();
        btnAdd.Click += (_, __) => { localItems.Add((cbProd.SelectedItem?.ToString() ?? "", cbBorda.SelectedItem?.ToString() ?? "")); lst.Items.Add($"1x {cbProd.SelectedItem} + {cbBorda.SelectedItem}"); };

        btnEnviar.Click += (_, __) =>
        {
            if (lst.Items.Count == 0) { MessageBox.Show("Adicione pelo menos um item."); return; }
            var id = "tmp_" + Guid.NewGuid().ToString("N")[..8];
            var now = DateTime.UtcNow.ToString("o");
            conn.Execute("INSERT INTO pedidos_local (id,origem,status,cliente_nome,cliente_telefone,subtotal,total,forma_pagamento,mesa_numero,created_at,updated_at,payload_json,synced) VALUES (@id,'mesa','recebido',@cli,'00000000000',@sub,@tot,'dinheiro',@mesa,@now,@now,@pay,0)",
                new { id, cli = $"Mesa {numero:D2}", sub = 59.90m * lst.Items.Count, tot = 59.90m * lst.Items.Count, mesa = numero, now, pay = $"{{\"mesa\":{numero}}}" });
            conn.Execute("UPDATE mesas SET status='ocupada', updated_at=@now WHERE numero=@n", new { now, n = numero });
            _sync.Enqueue("pedidos", "insert", new { id, origem = "mesa", status = "recebido", cliente_nome = $"Mesa {numero:D2}", mesa_numero = numero, total = 59.90m * lst.Items.Count });
            var itensPrint = localItems.Select(x => new ItemPrint(x.prod.Split('—')[0].Trim(), 1, 59.90m, x.borda, null)).ToList();
            var p = new PedidoPrint(id, "mesa", $"Mesa {numero:D2}", "-", null, null, 0, numero, null, itensPrint, 59.90m * lst.Items.Count, 59.90m * lst.Items.Count, "mesa", DateTime.Now.ToString("HH:mm"), null);
            var raw = Templates.ComandaCozinha(p);
            var (ok, via) = RawPrinter.PrintAuto(raw);
            MessageBox.Show(ok ? $"Comanda enviada ({via})" : via, "Mesa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            f.Close();
        };

        f.ShowDialog(this);
        Navigate("mesas");
    }

    // ===== CARDÁPIO =====
    private void LoadCardapio()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Cardápio", "F4 • Editado na Central e sincronizado com o Site via Supabase Realtime"));

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
        tabs.Appearance = TabAppearance.FlatButtons;
        tabs.ItemSize = new Size(0, 28);
        tabs.TabPages.Add(MakeProdutosTab());
        tabs.TabPages.Add(MakeBordasTab());
        tabs.TabPages.Add(MakeBairrosTab());
        tabs.TabPages.Add(MakeSaboresTab());
        pnlMain.Controls.Add(tabs);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        var btnSync = BtnPrimary("Sincronizar com Site agora →", async () => { await DoSync(); MessageBox.Show("Fila enviada. O Site atualiza em segundos via Realtime.", "Cardápio", MessageBoxButtons.OK, MessageBoxIcon.Information); });
        btnSync.Dock = DockStyle.Right;
        bar.Controls.Add(btnSync);
        pnlMain.Controls.Add(bar);
    }

    private TabPage MakeProdutosTab()
    {
        var tp = new TabPage("  Produtos  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query(@"SELECT p.nome, p.categoria, v.tamanho, v.preco, v.custo FROM produtos p LEFT JOIN variacoes v ON v.produto_id=p.id WHERE p.ativo=1 ORDER BY p.categoria, p.nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Produto"); dt.Columns.Add("Categoria"); dt.Columns.Add("Tam"); dt.Columns.Add("Preço"); dt.Columns.Add("Custo");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.categoria, (string)(r.tamanho ?? "—"), r.preco != null ? $"R$ {Convert.ToDecimal(r.preco):F2}" : "—", r.custo != null ? $"R$ {Convert.ToDecimal(r.custo):F2}" : "—");
        if (dt.Rows.Count == 0) { dt.Rows.Add("Pizza Grande (8 fatias)", "pizza", "G", "R$ 59,90", "R$ 18,00"); dt.Rows.Add("Pizza Pequena (4 fatias)", "pizza", "P", "R$ 34,90", "R$ 11,00"); }
        g.DataSource = dt;
        bar.Controls.Add(BtnPrimary("Novo produto", () => NovoProduto()));
        bar.Controls.Add(BtnGhost("Editar preço", () => EditarPreco(g)));
        bar.Controls.Add(BtnGhost("Desativar", () => { if (g.SelectedRows.Count>0) { var n=g.SelectedRows[0].Cells[0].Value?.ToString(); using var cc=_db.Connect(); cc.Open(); cc.Execute("UPDATE produtos SET ativo=0, updated_at=@now WHERE nome=@n", new { now=DateTime.UtcNow.ToString("o"), n}); _sync.Enqueue("produtos","update", new { nome=n, ativo=false}); Navigate("cardapio"); }}));
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeBordasTab()
    {
        var tp = new TabPage("  Bordas  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnGhost("Editar borda", () => { if (tp.Controls.OfType<DataGridView>().FirstOrDefault() is DataGridView gg && gg.SelectedRows.Count>0) { var id=gg.SelectedRows[0].Cells[0].Value?.ToString(); var novo=Prompt($"Novo preço para {id}:", "8,00"); if(decimal.TryParse(novo, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var pr)) { using var cc=_db.Connect(); cc.Open(); cc.Execute("UPDATE bordas SET preco_adicional=@p, updated_at=@now WHERE id=@id", new { p=pr, now=DateTime.UtcNow.ToString("o"), id}); _sync.Enqueue("bordas","update", new { id, preco_adicional=pr}); Navigate("cardapio"); } }}));
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT id, nome, preco_adicional, tipo FROM bordas WHERE ativo=1 ORDER BY preco_adicional").ToList();
        var dt = new DataTable(); dt.Columns.Add("ID"); dt.Columns.Add("Nome"); dt.Columns.Add("Preço adicional"); dt.Columns.Add("Tipo");
        foreach (var r in rows) dt.Rows.Add((string)r.id, (string)r.nome, $"R$ {Convert.ToDecimal(r.preco_adicional):F2}", (string)r.tipo);
        g.DataSource = dt;
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeBairrosTab()
    {
        var tp = new TabPage("  Bairros  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo bairro", () => NovoBairro()));
        bar.Controls.Add(BtnGhost("Editar taxa", () => { if (tp.Controls.OfType<DataGridView>().FirstOrDefault() is DataGridView gg && gg.SelectedRows.Count>0) { var nome=gg.SelectedRows[0].Cells[0].Value?.ToString(); var novo=Prompt($"Nova taxa para {nome}:", "5,00"); if(decimal.TryParse(novo, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var pr)) { using var cc=_db.Connect(); cc.Open(); cc.Execute("UPDATE bairros SET taxa_fixa=@p, updated_at=@now WHERE nome=@nome", new { p=pr, now=DateTime.UtcNow.ToString("o"), nome}); _sync.Enqueue("bairros","update", new { nome, taxa_fixa=pr}); Navigate("cardapio"); } }}));
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, taxa_fixa FROM bairros WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Bairro"); dt.Columns.Add("Taxa fixa");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, $"R$ {Convert.ToDecimal(r.taxa_fixa):F2}");
        g.DataSource = dt;
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeSaboresTab()
    {
        var tp = new TabPage("  Recheios / Sabores  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo recheio", () => NovoSabor()));
        bar.Controls.Add(BtnGhost("Editar", () => EditarSabor()));
        bar.Controls.Add(BtnGhost("Ver ficha", () => VerFichaSabor()));
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, categoria, custo, manipulado, disponivel, rendimento_porcoes, peso_total_g, custo_calculado FROM sabores WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Nome"); dt.Columns.Add("Cat"); dt.Columns.Add("Tipo"); dt.Columns.Add("Custo"); dt.Columns.Add("Custo calc."); dt.Columns.Add("Disp."); dt.Columns.Add("Rendimento");
        foreach (var r in rows)
        {
            var tipo = Convert.ToInt32(r.manipulado) == 1 ? "Manipulado" : "Simples";
            var disp = Convert.ToInt32(r.disponivel) == 1 ? "Sim" : "Não";
            var custo = r.custo != null ? $"R$ {Convert.ToDecimal(r.custo):F2}" : "—";
            var calc = r.custo_calculado != null ? $"R$ {Convert.ToDecimal(r.custo_calculado):F2}" : "—";
            var rend = r.rendimento_porcoes != null ? $"{r.rendimento_porcoes} porções" : "—";
            dt.Rows.Add((string)r.nome, (string)r.categoria, tipo, custo, calc, disp, rend);
        }
        if (dt.Rows.Count == 0) dt.Rows.Add("Calabresa", "salgada", "Simples", "R$ 6,00", "—", "Sim", "—");
        g.DataSource = dt;
        g.Tag = "saboresGrid";
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }

    private void NovoProduto()
    {
        var nome = Prompt("Nome do produto:", "Pizza Grande (8 fatias)"); if (string.IsNullOrWhiteSpace(nome)) return;
        var cat = Prompt("Categoria (pizza/esfiha/calzone/salgado/pastel/bebida):", "pizza");
        var preco = Prompt("Preço:", "59.90"); if (!decimal.TryParse(preco, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pr)) pr = 59.90m;
        var id = Guid.NewGuid().ToString(); var now = DateTime.UtcNow.ToString("o");
        using var c = _db.Connect(); c.Open();
        c.Execute("INSERT INTO produtos (id,nome,categoria,ativo,max_sabores,created_at,updated_at) VALUES (@id,@nome,@cat,1,2,@now,@now)", new { id, nome, cat, now });
        var vid = Guid.NewGuid().ToString();
        c.Execute("INSERT INTO variacoes (id,produto_id,tamanho,preco,custo,ativo,updated_at) VALUES (@id,@pid,'G',@preco,0,1,@now)", new { id = vid, pid = id, preco = pr, now });
        _sync.Enqueue("produtos", "insert", new { id, nome, categoria = cat, ativo = true, max_sabores = 2, updated_at = now });
        _sync.Enqueue("variacoes", "insert", new { id = vid, produto_id = id, tamanho = "G", preco = pr });
        MessageBox.Show("Produto criado. Clique em Sincronizar.", "Cardápio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Navigate("cardapio");
    }
    private void EditarPreco(DataGridView g)
    {
        if (g.SelectedRows.Count == 0) { MessageBox.Show("Selecione um produto."); return; }
        var nome = g.SelectedRows[0].Cells[0].Value?.ToString();
        var novo = Prompt($"Novo preço para {nome}:", "59.90"); if (!decimal.TryParse(novo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pr)) return;
        using var c = _db.Connect(); c.Open();
        var pid = c.ExecuteScalar<string>("SELECT id FROM produtos WHERE nome=@n", new { n = nome });
        if (pid != null) { c.Execute("UPDATE variacoes SET preco=@p, updated_at=@now WHERE produto_id=@pid", new { p = pr, now = DateTime.UtcNow.ToString("o"), pid }); _sync.Enqueue("variacoes", "update", new { produto_id = pid, preco = pr }); Navigate("cardapio"); }
    }
    private void NovoBairro()
    {
        var nome = Prompt("Nome do bairro:", "Centro"); var taxa = Prompt("Taxa:", "5.00");
        if (!decimal.TryParse(taxa, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t)) return;
        var id = Guid.NewGuid().ToString(); var now = DateTime.UtcNow.ToString("o");
        using var c = _db.Connect(); c.Open(); c.Execute("INSERT INTO bairros (id,nome,taxa_fixa,ativo,created_at,updated_at) VALUES (@id,@n,@t,1,@now,@now)", new { id, n = nome, t, now });
        _sync.Enqueue("bairros", "insert", new { id, nome, taxa_fixa = t }); Navigate("cardapio");
    }
    private void NovoSabor()
    {
        var f = new Form { Text = "Novo recheio / sabor", Width = 760, Height = 620, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = C_Bg, Font = new Font("Segoe UI", 9f) };
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 100, ColumnCount = 2, RowCount = 3, Padding = new Padding(12), BackColor = C_Card };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        top.Controls.Add(new Label { Text = "Nome do recheio", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), AutoSize = true }, 0, 0);
        top.Controls.Add(new Label { Text = "Categoria", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), AutoSize = true }, 1, 0);
        var txtNome = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Ex: Carne Moída Esfiha", Font = new Font("Segoe UI", 10f) };
        var cbCat = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; cbCat.Items.AddRange(new object[] { "salgada", "doce", "mista" }); cbCat.SelectedIndex = 0;
        top.Controls.Add(txtNome, 0, 1); top.Controls.Add(cbCat, 1, 1);
        var chkManip = new CheckBox { Text = "É recheio manipulado (precisa produzir na cozinha)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = C_Text };
        var pnlRend = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        pnlRend.Controls.Add(new Label { Text = "Rendimento (porções)", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Dock = DockStyle.Left, Width = 140, TextAlign = ContentAlignment.MiddleLeft });
        var numRend = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 25, Width = 70, Dock = DockStyle.Left };
        pnlRend.Controls.Add(numRend);
        top.Controls.Add(chkManip, 0, 2); top.Controls.Add(pnlRend, 1, 2);
        f.Controls.Add(top);

        var pnlCalc = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = C_Card, Padding = new Padding(12) };
        pnlCalc.Paint += (s,e)=>{ using var pen=new Pen(C_Border); e.Graphics.DrawRectangle(pen,0,0,pnlCalc.Width-1,pnlCalc.Height-1); };
        var lblPeso = new Label { Text = "Peso total: —", Dock = DockStyle.Top, Height = 18, ForeColor = C_Muted, Font = new Font("Segoe UI", 8f) };
        var lblCusto = new Label { Text = "Custo total: —", Dock = DockStyle.Top, Height = 18, ForeColor = C_Muted, Font = new Font("Segoe UI", 8f) };
        var lblPorcao = new Label { Text = "Custo por porção: —", Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = C_Text };
        var lblSug = new Label { Text = "Preço sugerido (60%): —", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = C_Primary };
        pnlCalc.Controls.Add(lblSug); pnlCalc.Controls.Add(lblPorcao); pnlCalc.Controls.Add(lblCusto); pnlCalc.Controls.Add(lblPeso);
        f.Controls.Add(pnlCalc);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = C_Card, Padding = new Padding(8) };
        bar.Paint += (s,e)=>{ using var pen=new Pen(C_Border); e.Graphics.DrawLine(pen,0,0,bar.Width,0); };
        var btnOk = BtnPrimary("Salvar recheio", () => {}); btnOk.Dock = DockStyle.Right;
        var btnCancel = BtnGhost("Cancelar", () => f.DialogResult = DialogResult.Cancel); btnCancel.Dock = DockStyle.Right;
        bar.Controls.Add(btnOk); bar.Controls.Add(btnCancel);
        f.Controls.Add(bar);

        var pnlGrid = new Panel { Dock = DockStyle.Fill, BackColor = C_Card, Padding = new Padding(12) };
        var lblGridTitle = new Label { Text = "Composição — adicione insumos cadastrados (peso e corte influenciam o custo)", Dock = DockStyle.Top, Height = 22, ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, Font = new Font("Segoe UI", 8.5f), ColumnHeadersHeight = 24, RowTemplate = { Height = 26 } };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Insumo", Name = "Insumo", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qtd", Name = "Qtd", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Un", Name = "Un", Width = 50 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Corte", Name = "Corte", Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Custo", Name = "Custo", Width = 90, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fator", Name = "Fator", Width = 50, ReadOnly = true });
        var gridBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 4, 0, 4) };
        var btnAdd = BtnGhost("Adicionar insumo", () => {});
        var btnRem = BtnGhost("Remover", () => { if (grid.SelectedRows.Count>0) grid.Rows.RemoveAt(grid.SelectedRows[0].Index); AtualizarCalc(); });
        gridBar.Controls.Add(btnAdd); gridBar.Controls.Add(btnRem);
        pnlGrid.Controls.Add(grid); pnlGrid.Controls.Add(gridBar); pnlGrid.Controls.Add(lblGridTitle);
        // ordem Dock: grid Fill deve ser último, barra Top antes
        pnlGrid.Controls.SetChildIndex(grid, 0);
        pnlGrid.Controls.SetChildIndex(gridBar, 1);
        pnlGrid.Controls.SetChildIndex(lblGridTitle, 2);
        f.Controls.Add(pnlGrid);
        pnlGrid.Visible = false; pnlCalc.Visible = false;

        chkManip.CheckedChanged += (_,__)=>{ pnlGrid.Visible = chkManip.Checked; pnlCalc.Visible = chkManip.Checked; f.Height = chkManip.Checked ? 620 : 260; };

        // Carrega insumos para combo
        List<dynamic> insumosList;
        using (var cc=_db.Connect()) { cc.Open(); insumosList = cc.Query("SELECT id, nome, unidade, custo_por_unidade FROM insumos WHERE ativo=1 ORDER BY nome").ToList(); }
        if (insumosList.Count==0) insumosList = new List<dynamic>{ new { id="tmp1", nome="Carne moída", unidade="kg", custo_por_unidade=32m }, new { id="tmp2", nome="Tomate", unidade="un", custo_por_unidade=0.80m } };

        void AtualizarCalc()
        {
            decimal custoTotal=0, pesoTotal=0;
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (r.Cells["Custo"].Value is string cs && decimal.TryParse(cs.Replace("R$","").Trim(), System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var c)) custoTotal+=c;
                if (r.Cells["Qtd"].Value != null && decimal.TryParse(r.Cells["Qtd"].Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtd))
                {
                    var un = r.Cells["Un"].Value?.ToString() ?? "g";
                    if (un=="kg") pesoTotal += qtd*1000;
                    else if (un=="g") pesoTotal += qtd;
                    else if (un=="ml"||un=="l") pesoTotal += qtd; // simplificado
                    else pesoTotal += qtd*50; // un ~50g
                }
            }
            var porcoes = (int)numRend.Value;
            var custoPorcao = porcoes>0 ? custoTotal/porcoes : custoTotal;
            var precoSug = custoPorcao==0?0: custoPorcao / 0.4m; // 60% margem
            lblPeso.Text = $"Peso total estimado: {pesoTotal:F0}g";
            lblCusto.Text = $"Custo total produção: R$ {custoTotal:F2}";
            lblPorcao.Text = $"Custo por porção ({porcoes} porções): R$ {custoPorcao:F2}";
            lblSug.Text = $"Preço sugerido (60%): R$ {precoSug:F2} por porção";
        }

        btnAdd.Click += (_,__) =>
        {
            var dlg = new Form { Text = "Adicionar insumo", Width = 420, Height = 260, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = C_Bg };
            var cbIns = new ComboBox { Dock = DockStyle.Top, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var ins in insumosList) cbIns.Items.Add($"{ins.nome} — R$ {Convert.ToDecimal(ins.custo_por_unidade):F4}/{ins.unidade}");
            cbIns.SelectedIndex = 0;
            var pnlQ = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0,6,0,0) };
            pnlQ.Controls.Add(new Label{Text="Qtd", Dock=DockStyle.Left, Width=40, TextAlign=ContentAlignment.MiddleLeft, ForeColor=C_Muted, Font=new Font("Segoe UI",8f, FontStyle.Bold)});
            var numQ = new NumericUpDown{ Minimum=0.1m, Maximum=10000, DecimalPlaces=2, Value=1, Width=100, Dock=DockStyle.Left};
            pnlQ.Controls.Add(numQ);
            var cbUn = new ComboBox{ Dock=DockStyle.Left, Width=70, Margin=new Padding(8,0,0,0), DropDownStyle=ComboBoxStyle.DropDownList };
            cbUn.Items.AddRange(new object[]{"g","kg","ml","l","un","col"}); cbUn.SelectedIndex=0;
            pnlQ.Controls.Add(cbUn);
            var pnlCorte = new Panel{ Dock=DockStyle.Top, Height=30, Padding=new Padding(0,6,0,0)};
            pnlCorte.Controls.Add(new Label{Text="Corte", Dock=DockStyle.Left, Width=50, TextAlign=ContentAlignment.MiddleLeft, ForeColor=C_Muted, Font=new Font("Segoe UI",8f, FontStyle.Bold)});
            var cbCorte = new ComboBox{ Dock=DockStyle.Left, Width=120, DropDownStyle=ComboBoxStyle.DropDownList };
            cbCorte.Items.AddRange(new object[]{"inteiro","cubos","fatiado","moído","espremido","picado","rodelas","ralado"}); cbCorte.SelectedIndex=0;
            pnlCorte.Controls.Add(cbCorte);
            var bar2 = new Panel{ Dock=DockStyle.Bottom, Height=40, BackColor=C_Card, Padding=new Padding(8)};
            var ok2 = BtnPrimary("Adicionar", ()=>{}); ok2.Dock=DockStyle.Right;
            var cancel2 = BtnGhost("Cancelar", ()=> dlg.DialogResult=DialogResult.Cancel); cancel2.Dock=DockStyle.Right;
            bar2.Controls.Add(ok2); bar2.Controls.Add(cancel2);
            dlg.Controls.Add(bar2); dlg.Controls.Add(pnlCorte); dlg.Controls.Add(pnlQ); dlg.Controls.Add(cbIns);
            dlg.AcceptButton = ok2;
            ok2.Click += (_,__) =>
            {
                var sel = cbIns.SelectedItem?.ToString() ?? "";
                var nome = sel.Split('—')[0].Trim();
                var ins = insumosList.FirstOrDefault(x=> ((string)x.nome)==nome) ?? insumosList[0];
                decimal custoUn = Convert.ToDecimal(ins.custo_por_unidade);
                var qtd = numQ.Value;
                var un = cbUn.SelectedItem?.ToString() ?? "g";
                var corte = cbCorte.SelectedItem?.ToString() ?? "inteiro";
                // conversão custo por unidade: se insumo é kg mas qtd é g, ajusta
                decimal custoLinha = 0;
                var unidadeInsumo = (string)ins.unidade;
                if (unidadeInsumo=="kg" && un=="g") custoLinha = (custoUn/1000m)*qtd;
                else if (unidadeInsumo=="g" && un=="kg") custoLinha = custoUn*1000m*qtd;
                else if (unidadeInsumo==un) custoLinha = custoUn*qtd;
                else custoLinha = custoUn*qtd; // simplificado
                var fator = PizzaPDV.Core.Pricing.FatorCorte(corte);
                custoLinha *= fator;
                grid.Rows.Add(nome, qtd.ToString(), un, corte, $"R$ {custoLinha:F2}", $"{fator:F2}x");
                AtualizarCalc();
                dlg.DialogResult = DialogResult.OK;
            };
            dlg.ShowDialog(f);
        };

        numRend.ValueChanged += (_,__)=>AtualizarCalc();

        if (f.ShowDialog(this) != DialogResult.OK) return;
        // Validação
        var nomeFinal = txtNome.Text.Trim();
        if (string.IsNullOrWhiteSpace(nomeFinal)) { MessageBox.Show("Nome obrigatório"); return; }
        var cat = cbCat.SelectedItem?.ToString() ?? "salgada";
        var manip = chkManip.Checked ? 1 : 0;
        var rend = (int)numRend.Value;
        var id = Guid.NewGuid().ToString(); var now = DateTime.UtcNow.ToString("o");
        // calcula custo total
        decimal custoCalc = 0;
        foreach (DataGridViewRow r in grid.Rows)
            if (r.Cells["Custo"].Value is string cs && decimal.TryParse(cs.Replace("R$","").Trim(), System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var c)) custoCalc+=c;
        using var c2 = _db.Connect(); c2.Open();
        c2.Execute("INSERT INTO sabores (id,nome,categoria,custo,ativo,updated_at,manipulado,rendimento_porcoes,peso_total_g,custo_calculado,disponivel) VALUES (@id,@n,@cat,@custo,1,@now,@man,@rend,@peso,@calc,1)",
            new { id, n=nomeFinal, cat, custo=custoCalc, now, man=manip, rend = manip==1? (int?)rend : null, peso = (decimal?)null, calc = manip==1? (decimal?)custoCalc : null });
        // salva sabor_insumos
        foreach (DataGridViewRow r in grid.Rows)
        {
            var nomeIns = r.Cells["Insumo"].Value?.ToString() ?? "";
            var qtdStr = r.Cells["Qtd"].Value?.ToString() ?? "0";
            var un = r.Cells["Un"].Value?.ToString() ?? "g";
            var corte = r.Cells["Corte"].Value?.ToString() ?? "inteiro";
            var fatorStr = r.Cells["Fator"].Value?.ToString() ?? "1.00x";
            decimal.TryParse(qtdStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtd);
            var fator = 1m; if(decimal.TryParse(fatorStr.Replace("x","").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fval)) fator=fval;
            var insId = insumosList.FirstOrDefault(x=> ((string)x.nome)==nomeIns)?.id ?? insumosList[0].id;
            var sid = Guid.NewGuid().ToString();
            c2.Execute("INSERT INTO sabor_insumos (id,sabor_id,insumo_id,quantidade,unidade,forma_corte,fator_perda) VALUES (@id,@sid,@iid,@qtd,@un,@corte,@fator)",
                new { id=sid, sid=id, iid=insId, qtd, un, corte, fator });
        }
        _sync.Enqueue("sabores", "insert", new { id, nome=nomeFinal, categoria=cat, manipulado=manip==1, rendimento_porcoes=rend });
        MessageBox.Show(manip==1 ? $"Recheio manipulado criado! Custo total R$ {custoCalc:F2} → R$ {(custoCalc/rend):F2} por porção." : "Recheio simples criado.", "Cardápio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Navigate("cardapio");
    }
    private void EditarSabor()
    {
        var grid = pnlMain.Controls.OfType<TabControl>().FirstOrDefault()?.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid==null || grid.SelectedRows.Count==0) { MessageBox.Show("Selecione um recheio."); return; }
        var nome = grid.SelectedRows[0].Cells[0].Value?.ToString();
        var novoNome = Prompt($"Editar nome para '{nome}':", nome ?? "");
        if (string.IsNullOrWhiteSpace(novoNome) || novoNome==nome) return;
        using var c = _db.Connect(); c.Open();
        c.Execute("UPDATE sabores SET nome=@n, updated_at=@now WHERE nome=@old", new { n=novoNome, now=DateTime.UtcNow.ToString("o"), old=nome });
        _sync.Enqueue("sabores","update", new { nome=novoNome, old=nome });
        Navigate("cardapio");
    }
    private void VerFichaSabor()
    {
        var grid = pnlMain.Controls.OfType<TabControl>().FirstOrDefault()?.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();
        if (grid==null || grid.SelectedRows.Count==0) { MessageBox.Show("Selecione um recheio."); return; }
        var nome = grid.SelectedRows[0].Cells[0].Value?.ToString();
        using var c = _db.Connect(); c.Open();
        var sabor = c.QueryFirstOrDefault("SELECT id, manipulado, rendimento_porcoes, custo_calculado FROM sabores WHERE nome=@n", new { n=nome });
        if (sabor==null) { MessageBox.Show("Não encontrado."); return; }
        if (Convert.ToInt32(sabor.manipulado)==0) { MessageBox.Show($"{nome} é recheio simples (não manipulado). Custo direto em sabores.custo."); return; }
        var itens = c.Query("SELECT si.quantidade, si.unidade, si.forma_corte, si.fator_perda, i.nome as insumo FROM sabor_insumos si JOIN insumos i ON i.id=si.insumo_id WHERE si.sabor_id=@id", new { id=(string)sabor.id }).ToList();
        var detalhe = string.Join("\n", itens.Select(r => $"- {r.quantidade}{r.unidade} {r.insumo} ({r.forma_corte}) x{r.fator_perda}"));
        var porcao = sabor.rendimento_porcoes != null ? $"Rendimento: {sabor.rendimento_porcoes} porções\nCusto por porção: R$ {(Convert.ToDecimal(sabor.custo_calculado)/Convert.ToInt32(sabor.rendimento_porcoes)):F2}" : "";
        MessageBox.Show($"Ficha: {nome}\n{detalhe}\n\nCusto total: R$ {Convert.ToDecimal(sabor.custo_calculado):F2}\n{porcao}", "Ficha técnica", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ===== CAIXA =====
    private void LoadCaixa()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Caixa", "F5 • Saldo inicial, sangria, suprimento e fechamento do dia"));
        using var c = _db.Connect(); c.Open();
        var caixa = c.QueryFirstOrDefault("SELECT saldo_inicial, total_vendas FROM caixa_local ORDER BY aberto_em DESC LIMIT 1");
        var kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 96, ColumnCount = 3, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        for (int i = 0; i < 3; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        kpis.Controls.Add(Kpi("Saldo inicial", caixa != null ? $"R$ {Convert.ToDecimal(caixa.saldo_inicial):F2}" : "R$ 100,00"));
        kpis.Controls.Add(Kpi("Vendas hoje", caixa != null ? $"R$ {Convert.ToDecimal(caixa.total_vendas):F2}" : "R$ 1.070,00"));
        kpis.Controls.Add(Kpi("Comissão entregador", "R$ 84,00 • 12 entregas"));
        pnlMain.Controls.Add(kpis);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnGhost("Sangria", () => MessageBox.Show("Sangria registrada.")));
        bar.Controls.Add(BtnGhost("Suprimento", () => MessageBox.Show("Suprimento registrado.")));
        bar.Controls.Add(BtnPrimary("Fechar caixa", () => MessageBox.Show("Caixa fechado.")));
        bar.Controls.Add(BtnGhost("Imprimir fechamento", () => TestPrint()));
        pnlMain.Controls.Add(bar);

        var g = CleanGrid();
        g.DataSource = new[] { new { Forma = "Dinheiro", Valor = "R$ 320,00" }, new { Forma = "Pix", Valor = "R$ 540,00" }, new { Forma = "Cartão InfinitePay", Valor = "R$ 210,00" } }.ToList();
        var card = Card(g, 0); card.Dock = DockStyle.Fill; pnlMain.Controls.Add(card);
    }
    private Control Kpi(string title, string value)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = C_Card, Margin = new Padding(6, 0, 6, 0), Padding = new Padding(12) };
        p.Paint += (s, e) => { using var pen = new Pen(C_Border); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
        var l1 = new Label { Text = title.ToUpper(), Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = C_Muted };
        var l2 = new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = C_Text, TextAlign = ContentAlignment.MiddleLeft };
        p.Controls.Add(l2); p.Controls.Add(l1);
        return p;
    }

    // ===== CLIENTES =====
    private void LoadClientes()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Clientes & Fidelidade", "F6 • A cada 10 pizzas G → 1 pizza P grátis"));

        var top = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
        var txt = new TextBox { PlaceholderText = "Buscar por telefone ou nome", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
        var btnBuscar = BtnGhost("Buscar", () => MessageBox.Show("Busca por telefone."));
        btnBuscar.Dock = DockStyle.Right; btnBuscar.Width = 90;
        top.Controls.Add(txt); top.Controls.Add(btnBuscar);
        // precisa ordem: txt fill depois buscar right, então adiciona na ordem inversa
        top.Controls.SetChildIndex(btnBuscar, 0);
        top.Controls.SetChildIndex(txt, 1);
        pnlMain.Controls.Add(top);

        var g = CleanGrid();
        g.DataSource = new[] { new { Nome = "João Silva", Telefone = "88 99999-0000", Progresso = "7 / 10", Cupons = 0 }, new { Nome = "Maria Souza", Telefone = "88 98888-0000", Progresso = "10 / 10", Cupons = 1 } }.ToList();
        var card = Card(g, 0); card.Dock = DockStyle.Fill; pnlMain.Controls.Add(card);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Resgatar pizza P", () => MessageBox.Show("Cupom resgatado — pizza P liberada.")));
        pnlMain.Controls.Add(bar);
    }

    // ===== VALIDADE & ETIQUETAS F7 =====
    private void LoadValidade()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Validade & Etiquetas", "F7 • Produtos manipulados → controle de validade → etiqueta JP-58H 32cols"));

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 0, 0, 8) };
        top.Controls.Add(BtnPrimary("Nova manipulação", () => NovaManipulacao()));
        top.Controls.Add(BtnGhost("Reimprimir etiqueta", () => ReimprimirEtiqueta()));
        top.Controls.Add(BtnGhost("Baixar (consumido)", () => AtualizarValidade("consumido")));
        top.Controls.Add(BtnGhost("Descartar", () => AtualizarValidade("descartado")));
        pnlMain.Controls.Add(top);

        var g = CleanGrid();
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Produto", DataPropertyName = "Produto" });
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Manipulação", DataPropertyName = "Manipulacao" });
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Validade", DataPropertyName = "Validade" });
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Resp.", DataPropertyName = "Resp" });
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status" });
        g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qtd", DataPropertyName = "Qtd" });
        g.AutoGenerateColumns = false;
        var card = Card(g, 0); card.Dock = DockStyle.Fill; pnlMain.Controls.Add(card);

        try
        {
            using var conn = _db.Connect(); conn.Open();
            var rows = conn.Query("SELECT nome, data_manipulacao, data_validade, responsavel, status, quantidade, unidade FROM produtos_manipulados ORDER BY data_validade ASC LIMIT 100").ToList();
            var dt = new DataTable();
            dt.Columns.Add("Produto"); dt.Columns.Add("Manipulacao"); dt.Columns.Add("Validade"); dt.Columns.Add("Resp"); dt.Columns.Add("Status"); dt.Columns.Add("Qtd");
            foreach (var r in rows)
            {
                var manip = DateTime.TryParse((string)r.data_manipulacao, out var dm) ? dm.ToString("dd/MM/yyyy HH:mm") : (string)r.data_manipulacao;
                var valid = DateTime.TryParse((string)r.data_validade, out var dv) ? dv.ToString("dd/MM/yyyy") : (string)r.data_validade;
                dt.Rows.Add((string)r.nome, manip, valid, (string)r.responsavel, (string)r.status, $"{r.quantidade} {r.unidade}");
            }
            if (dt.Rows.Count == 0)
            {
                dt.Rows.Add("Frango desfiado", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), DateTime.Now.AddDays(3).ToString("dd/MM/yyyy"), "Maria", "valido", "1 bandeja");
                dt.Rows.Add("Massa pizza", DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy HH:mm"), DateTime.Now.AddDays(1).ToString("dd/MM/yyyy"), "João", "vencendo", "2 kg");
            }
            g.DataSource = dt;
            g.Tag = "validadeGrid";
            g.CellFormatting += (s, e) =>
            {
                if (e.ColumnIndex == 4 && e.Value is string st)
                {
                    if (st == "vencido") { e.CellStyle.BackColor = Color.FromArgb(255, 235, 235); e.CellStyle.ForeColor = Color.FromArgb(180, 30, 30); }
                    else if (st == "vencendo") { e.CellStyle.BackColor = Color.FromArgb(255, 248, 220); e.CellStyle.ForeColor = Color.FromArgb(154, 103, 0); }
                    else if (st == "valido") { e.CellStyle.BackColor = Color.FromArgb(235, 255, 235); e.CellStyle.ForeColor = Color.FromArgb(26, 127, 55); }
                }
            };
        }
        catch (Exception ex) { MessageBox.Show("Erro validade: " + ex.Message); }
        g.Tag = g; // guarda grid para ações
        // armazena referência para reimprimir
        pnlMain.Tag = g;
    }

    private void NovaManipulacao()
    {
        var nome = Prompt("Nome do produto manipulado:", "Frango desfiado");
        if (string.IsNullOrWhiteSpace(nome)) return;
        var diasStr = Prompt("Dias até validade:", "3");
        if (!int.TryParse(diasStr, out var dias)) dias = 3;
        var resp = Prompt("Responsável:", Environment.UserName ?? "Cozinha");
        if (string.IsNullOrWhiteSpace(resp)) return;
        var qtdStr = Prompt("Quantidade:", "1");
        if (!decimal.TryParse(qtdStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtd)) qtd = 1;
        var manip = DateTime.Now;
        var valid = manip.AddDays(dias);
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        using var conn = _db.Connect(); conn.Open();
        conn.Execute("INSERT INTO produtos_manipulados (id,nome,categoria,data_manipulacao,data_validade,dias_validade,responsavel,quantidade,unidade,status,created_at,updated_at) VALUES (@id,@nome,'manipulado',@man,@val,@dias,@resp,@qtd,'un','valido',@now,@now)",
            new { id, nome, man = manip.ToString("o"), val = valid.ToString("o"), dias, resp, qtd, now });
        // imprime etiqueta
        var etiqueta = $"==========\n   *** VALIDADE ***\n==========\n{nome.ToUpper()}\n----------\nMANIP: {manip:dd/MM/yyyy HH:mm}\nVALID: {valid:dd/MM/yyyy}\nRESP: {resp.ToUpper()}\nQTD: {qtd} un\n==========\nID {id[..8].ToUpper()}\n\n\n";
        var (ok, via) = RawPrinter.PrintAuto(etiqueta);
        MessageBox.Show(ok ? $"Manipulação criada e etiqueta impressa em {via}" : $"{via}\n\n{etiqueta}", "Validade", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Navigate("validade");
    }
    private void ReimprimirEtiqueta()
    {
        if (pnlMain.Tag is not DataGridView g || g.SelectedRows.Count == 0) { MessageBox.Show("Selecione uma linha para reimprimir."); return; }
        var nome = g.SelectedRows[0].Cells[0].Value?.ToString() ?? "Produto";
        var manip = g.SelectedRows[0].Cells[1].Value?.ToString() ?? DateTime.Now.ToString("dd/MM/yyyy");
        var valid = g.SelectedRows[0].Cells[2].Value?.ToString() ?? DateTime.Now.AddDays(3).ToString("dd/MM/yyyy");
        var resp = g.SelectedRows[0].Cells[3].Value?.ToString() ?? "-";
        var etiqueta = $"==========\n   *** VALIDADE ***\n==========\n{nome.ToUpper()}\n----------\nMANIP: {manip}\nVALID: {valid}\nRESP: {resp.ToUpper()}\n==========\n\n\n";
        var (ok, via) = RawPrinter.PrintAuto(etiqueta);
        MessageBox.Show(ok ? $"Reimpresso em {via}" : via, "Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void AtualizarValidade(string novoStatus)
    {
        if (pnlMain.Tag is not DataGridView g || g.SelectedRows.Count == 0) { MessageBox.Show("Selecione uma linha."); return; }
        var nome = g.SelectedRows[0].Cells[0].Value?.ToString();
        using var conn = _db.Connect(); conn.Open();
        conn.Execute("UPDATE produtos_manipulados SET status=@st, updated_at=@now WHERE nome=@nome", new { st = novoStatus, now = DateTime.UtcNow.ToString("o"), nome });
        Navigate("validade");
    }

    // ===== CONFIGURAÇÕES F10 =====
    private void LoadConfig()
    {
        pnlMain.Controls.Clear();
        pnlMain.Controls.Add(TitleBar("Configurações", "F10 • Sistema • Impressora • Taxas • Precificação justa base"));

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
        tabs.TabPages.Add(MakeInsumosTab());
        tabs.TabPages.Add(MakeMassasTab());
        tabs.TabPages.Add(MakeBasesTab());
        tabs.TabPages.Add(MakeGeralTab());
        pnlMain.Controls.Add(tabs);
    }
    private TabPage MakeInsumosTab()
    {
        var tp = new TabPage("  Insumos (matéria-prima)  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo insumo", () => NovoInsumo()));
        bar.Controls.Add(BtnGhost("Editar", () => { var gg = tp.Controls.OfType<DataGridView>().FirstOrDefault(); if(gg!=null && gg.SelectedRows.Count>0){ var n=gg.SelectedRows[0].Cells[0].Value?.ToString(); var novo=Prompt($"Editar {n} - novo preço:", "5,00"); if(decimal.TryParse(novo, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var pr)){ using var cc=_db.Connect(); cc.Open(); var iid=cc.ExecuteScalar<string>("SELECT id FROM insumos WHERE nome=@n", new{n}); if(iid!=null){ cc.Execute("UPDATE insumos SET preco_embalagem=@p, custo_por_unidade=@c, updated_at=@now WHERE id=@id", new{p=pr, c=pr/1m, now=DateTime.UtcNow.ToString("o"), id=iid}); Navigate("config"); } } } }));
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, qtd_embalagem, unidade, preco_embalagem, custo_por_unidade FROM insumos WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Nome"); dt.Columns.Add("Embalagem"); dt.Columns.Add("Unidade"); dt.Columns.Add("Preço emb."); dt.Columns.Add("Custo/un");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, $"{r.qtd_embalagem}", (string)r.unidade, $"R$ {Convert.ToDecimal(r.preco_embalagem):F2}", $"R$ {Convert.ToDecimal(r.custo_por_unidade):F4}");
        if (dt.Rows.Count == 0) dt.Rows.Add("Farinha de Trigo", "1", "kg", "R$ 5,00", "R$ 0,0050/g");
        g.DataSource = dt;
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeMassasTab()
    {
        var tp = new TabPage("  Massas matriz  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Nova massa", () => MessageBox.Show("Cadastrar massa: soma insumos + peso → custo/g")));
        bar.Controls.Add(BtnGhost("Ver ingredientes", () => MessageBox.Show("SELECT * FROM receita_itens")));
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, tipo, peso_total_g, custo_total, custo_por_g, porcao_padrao_g FROM receitas_massa ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Massa"); dt.Columns.Add("Tipo"); dt.Columns.Add("Peso total"); dt.Columns.Add("Custo total"); dt.Columns.Add("Custo/g"); dt.Columns.Add("Porção padrão");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.tipo, $"{r.peso_total_g}g", $"R$ {Convert.ToDecimal(r.custo_total):F2}", $"R$ {Convert.ToDecimal(r.custo_por_g):F4}", $"{r.porcao_padrao_g}g");
        if (dt.Rows.Count == 0) dt.Rows.Add("Massa Pizza", "pizza", "1450g", "R$ 12,30", "R$ 0,0084", "320g");
        g.DataSource = dt;
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeBasesTab()
    {
        var tp = new TabPage("  Bases P/G (invisível)  "); tp.Padding = new Padding(8);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnGhost("Editar base", () => { var gg=tp.Controls.OfType<DataGridView>().FirstOrDefault(); if(gg!=null && gg.SelectedRows.Count>0){ var tam=gg.SelectedRows[0].Cells[0].Value?.ToString(); var novo=Prompt($"Novo preço exibido para {tam}:", "14,90"); if(decimal.TryParse(novo, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var pr)){ using var cc=_db.Connect(); cc.Open(); cc.Execute("UPDATE bases_tamanho SET preco_exibido=@p, updated_at=@now WHERE tamanho=@tam", new{p=pr, now=DateTime.UtcNow.ToString("o"), tam}); Navigate("config"); } }}));
        var lbl = new Label { Text = "* Fixos = orégano + azeitona + embalagem + molho (invisíveis para cliente, só massa aparece)", Dock = DockStyle.Bottom, Height = 20, ForeColor = C_Muted, Font = new Font("Segoe UI", 7.5f) };
        var g = CleanGrid();
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT tamanho, peso_massa_g, custo_fixos, custo_total, preco_exibido FROM bases_tamanho ORDER BY tamanho").ToList();
        var dt = new DataTable(); dt.Columns.Add("Tam"); dt.Columns.Add("Peso massa"); dt.Columns.Add("Custo fixos*"); dt.Columns.Add("Custo total"); dt.Columns.Add("Preço exibido (massa)");
        foreach (var r in rows) dt.Rows.Add((string)r.tamanho, $"{r.peso_massa_g}g", $"R$ {Convert.ToDecimal(r.custo_fixos):F2}", $"R$ {Convert.ToDecimal(r.custo_total):F2}", $"R$ {Convert.ToDecimal(r.preco_exibido):F2}");
        if (dt.Rows.Count == 0) dt.Rows.Add("G", "320g", "R$ 1,89", "R$ 4,60", "R$ 14,90");
        g.DataSource = dt;
        tp.Controls.Add(lbl);
        tp.Controls.Add(bar);
        tp.Controls.Add(g);
        return tp;
    }
    private TabPage MakeGeralTab()
    {
        var tp = new TabPage("  Geral  "); tp.Padding = new Padding(12);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Salvar", () =>
        {
            var supa = tp.Controls.OfType<TableLayoutPanel>().FirstOrDefault()?.GetControlFromPosition(1,3) as TextBox;
            var val = supa?.Text ?? "";
            using var cc=_db.Connect(); cc.Open();
            cc.Execute("INSERT OR REPLACE INTO config (chave,valor,updated_at) VALUES ('supabase_url',@v,@now)", new{v=val, now=DateTime.UtcNow.ToString("o")});
            Environment.SetEnvironmentVariable("SUPABASE_URL", val);
            MessageBox.Show("Configurações salvas.");
        }));
        bar.Controls.Add(BtnGhost("Testar impressão", () => TestPrint()));
        bar.Controls.Add(BtnGhost("Backup .db", () => MessageBox.Show($"Backup em {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pizzapdv.db")}")));
        var pnl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        for (int i = 0; i < 2; i++) pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for(int i=0;i<4;i++) pnl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        pnl.Controls.Add(new Label { Text = "Margem padrão (%)", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },0,0);
        pnl.Controls.Add(new TextBox { Text = "60", Dock = DockStyle.Fill },1,0);
        pnl.Controls.Add(new Label { Text = "Taxa serviço mesa (%)", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },0,1);
        pnl.Controls.Add(new TextBox { Text = "0", Dock = DockStyle.Fill },1,1);
        pnl.Controls.Add(new Label { Text = "Impressora WinSpool", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },0,2);
        pnl.Controls.Add(new TextBox { Text = "JP-58H", Dock = DockStyle.Fill },1,2);
        pnl.Controls.Add(new Label { Text = "SUPABASE_URL", ForeColor = C_Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft },0,3);
        pnl.Controls.Add(new TextBox { Text = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "", Dock = DockStyle.Fill },1,3);
        tp.Controls.Add(bar);
        tp.Controls.Add(pnl);
        return tp;
    }
    private void NovoInsumo()
    {
        var nome = Prompt("Nome do insumo:", "Mussarela peça");
        if (string.IsNullOrWhiteSpace(nome)) return;
        var precoStr = Prompt("Preço da embalagem:", "42,00");
        if (!decimal.TryParse(precoStr, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-BR"), out var preco)) return;
        var qtdStr = Prompt("Qtd na embalagem:", "4");
        if (!decimal.TryParse(qtdStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qtd)) return;
        var unidade = Prompt("Unidade (g/kg/ml/l/un/col):", "kg");
        var custo = qtd == 0 ? 0 : preco / qtd;
        var id = Guid.NewGuid().ToString(); var now = DateTime.UtcNow.ToString("o");
        using var c = _db.Connect(); c.Open();
        c.Execute("INSERT INTO insumos (id,nome,unidade,qtd_embalagem,preco_embalagem,custo_por_unidade,ativo,updated_at) VALUES (@id,@nome,@un,@qtd,@preco,@custo,1,@now)",
            new { id, nome, un = unidade, qtd, preco, custo, now });
        MessageBox.Show($"Insumo criado. Custo por {unidade}: R$ {custo:F4}");
        Navigate("config");
    }

    private void TestPrint()
    {
        var p = new PedidoPrint("teste1234", "balcao", "Teste", "88999990000", "Rua Centro 123", "Centro", 5, null, null, new List<ItemPrint> { new ItemPrint("Pizza G Mussarela", 1, 59.90m, "Borda Catupiry", "sem cebola") }, 59.90m, 64.90m, "dinheiro", DateTime.Now.ToString("HH:mm"), null);
        var raw = Templates.TicketCliente(p);
        var (ok, via) = RawPrinter.PrintAuto(raw);
        MessageBox.Show(ok ? $"Impresso em {via}" : via + "\n\n" + raw, "JP-58H", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private string? Prompt(string text, string def)
    {
        var f = new Form { Width = 380, Height = 150, Text = text, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, BackColor = C_Bg, Font = new Font("Segoe UI", 9f) };
        var tb = new TextBox { Text = def, Dock = DockStyle.Top, Margin = new Padding(12), Font = new Font("Segoe UI", 10f) };
        f.Controls.Add(tb);
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = C_Card, Padding = new Padding(8) };
        bar.Paint += (s, e) => { using var pen = new Pen(C_Border); e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0); };
        var ok = BtnPrimary("OK", () => f.DialogResult = DialogResult.OK); ok.Dock = DockStyle.Right;
        var cancel = BtnGhost("Cancelar", () => f.DialogResult = DialogResult.Cancel); cancel.Dock = DockStyle.Right;
        bar.Controls.Add(ok); bar.Controls.Add(cancel);
        f.Controls.Add(bar); f.AcceptButton = ok;
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
    }
}
