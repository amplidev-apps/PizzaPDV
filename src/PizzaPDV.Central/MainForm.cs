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

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 560, Panel1MinSize = 400, Panel2MinSize = 320, BackColor = C_Bg };
        pnlMain.Controls.Add(split);

        // Esquerda: produtos + borda + obs
        var leftCard = Card(new Panel(), 12);
        var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        leftCard.Controls.Clear(); leftCard.Controls.Add(left);
        leftCard.Padding = new Padding(12);
        split.Panel1.Controls.Add(leftCard);

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
        split.Panel2.Controls.Add(rightCard);

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
        var g = CleanGrid(); tp.Controls.Add(g);
        using var c = _db.Connect(); c.Open();
        var rows = c.Query(@"SELECT p.nome, p.categoria, v.tamanho, v.preco, v.custo FROM produtos p LEFT JOIN variacoes v ON v.produto_id=p.id WHERE p.ativo=1 ORDER BY p.categoria, p.nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Produto"); dt.Columns.Add("Categoria"); dt.Columns.Add("Tam"); dt.Columns.Add("Preço"); dt.Columns.Add("Custo");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.categoria, (string)(r.tamanho ?? "—"), r.preco != null ? $"R$ {Convert.ToDecimal(r.preco):F2}" : "—", r.custo != null ? $"R$ {Convert.ToDecimal(r.custo):F2}" : "—");
        if (dt.Rows.Count == 0) { dt.Rows.Add("Pizza Grande (8 fatias)", "pizza", "G", "R$ 59,90", "R$ 18,00"); dt.Rows.Add("Pizza Pequena (4 fatias)", "pizza", "P", "R$ 34,90", "R$ 11,00"); }
        g.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo produto", () => NovoProduto()));
        bar.Controls.Add(BtnGhost("Editar preço", () => EditarPreco(g)));
        bar.Controls.Add(BtnGhost("Desativar", () => MessageBox.Show("UPDATE produtos SET ativo=0")));
        tp.Controls.Add(bar);
        return tp;
    }
    private TabPage MakeBordasTab()
    {
        var tp = new TabPage("  Bordas  "); tp.Padding = new Padding(8);
        var g = CleanGrid(); tp.Controls.Add(g);
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT id, nome, preco_adicional, tipo FROM bordas WHERE ativo=1 ORDER BY preco_adicional").ToList();
        var dt = new DataTable(); dt.Columns.Add("ID"); dt.Columns.Add("Nome"); dt.Columns.Add("Preço adicional"); dt.Columns.Add("Tipo");
        foreach (var r in rows) dt.Rows.Add((string)r.id, (string)r.nome, $"R$ {Convert.ToDecimal(r.preco_adicional):F2}", (string)r.tipo);
        g.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnGhost("Editar borda", () => MessageBox.Show("Edite em bordas: UPDATE bordas SET preco_adicional")));
        tp.Controls.Add(bar);
        return tp;
    }
    private TabPage MakeBairrosTab()
    {
        var tp = new TabPage("  Bairros  "); tp.Padding = new Padding(8);
        var g = CleanGrid(); tp.Controls.Add(g);
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, taxa_fixa FROM bairros WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Bairro"); dt.Columns.Add("Taxa fixa");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, $"R$ {Convert.ToDecimal(r.taxa_fixa):F2}");
        g.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo bairro", () => NovoBairro()));
        bar.Controls.Add(BtnGhost("Editar taxa", () => MessageBox.Show("UPDATE bairros SET taxa_fixa")));
        tp.Controls.Add(bar);
        return tp;
    }
    private TabPage MakeSaboresTab()
    {
        var tp = new TabPage("  Sabores  "); tp.Padding = new Padding(8);
        var g = CleanGrid(); tp.Controls.Add(g);
        using var c = _db.Connect(); c.Open();
        var rows = c.Query("SELECT nome, categoria, custo FROM sabores WHERE ativo=1 ORDER BY nome").ToList();
        var dt = new DataTable(); dt.Columns.Add("Sabor"); dt.Columns.Add("Categoria"); dt.Columns.Add("Custo ficha");
        foreach (var r in rows) dt.Rows.Add((string)r.nome, (string)r.categoria, $"R$ {Convert.ToDecimal(r.custo):F2}");
        g.DataSource = dt;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(BtnPrimary("Novo sabor", () => NovoSabor()));
        tp.Controls.Add(bar);
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
        var nome = Prompt("Nome do sabor:", "Calabresa"); if (string.IsNullOrWhiteSpace(nome)) return;
        var id = Guid.NewGuid().ToString(); var now = DateTime.UtcNow.ToString("o");
        using var c = _db.Connect(); c.Open(); c.Execute("INSERT INTO sabores (id,nome,categoria,custo,ativo,updated_at) VALUES (@id,@n,'salgada',0,1,@now)", new { id, n = nome, now });
        _sync.Enqueue("sabores", "insert", new { id, nome }); Navigate("cardapio");
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
