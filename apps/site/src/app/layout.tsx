import "./globals.css";
export const metadata = { title: "PizzaPDV - Cardápio", description: "Peça sua pizza online" };
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <body className="min-h-screen bg-zinc-50 text-zinc-900 antialiased">
        <header className="sticky top-0 z-50 bg-red-600 text-white shadow">
          <div className="mx-auto max-w-6xl px-4 py-3 flex items-center justify-between">
            <h1 className="text-xl font-bold">🍕 PizzaPDV</h1>
            <nav className="text-sm">Centro • Sítio • Delivery</nav>
          </div>
        </header>
        <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
        <footer className="border-t py-6 text-center text-sm text-zinc-500">Taxa por bairro • Pagamento: Pix / Dinheiro / InfinitePay</footer>
      </body>
    </html>
  );
}
