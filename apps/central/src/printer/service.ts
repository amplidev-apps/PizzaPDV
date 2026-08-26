// apps/central/src/printer/service.ts:1
import { comandaCozinha, ticketCliente, ticketDelivery, type PedidoPrint } from "./templates.js";

let printer: any = null;
async function getPrinter() {
  if (printer) return printer;
  try {
    const { ThermalPrinter, PrinterTypes } = await import("node-thermal-printer");
    printer = new ThermalPrinter({
      type: PrinterTypes.EPSON,
      interface: "printer:JP-58H", // nome da impressora no Windows
      width: 32,
      // SLOVENIA removido — tipagem exige PC852 etc, usa default
      removeSpecialCharacters: false,
      lineCharacter: "-",
    } as any);
    return printer;
  } catch {
    return null;
  }
}

export async function printTemplate(template: "cozinha" | "cliente" | "delivery", data: PedidoPrint) {
  let raw = "";
  if (template === "cozinha") raw = comandaCozinha(data);
  else if (template === "cliente") raw = ticketCliente(data);
  else raw = ticketDelivery(data);

  const p = await getPrinter();
  if (!p) {
    // Fallback: salva em arquivo para debug e retorna raw
    console.log("[printer mock JP-58H]\n" + raw);
    return { ok: true, mock: true, raw };
  }
  const isConnected = await p.isPrinterConnected();
  if (!isConnected) {
    console.warn("[printer] JP-58H não conectada, imprimindo mock");
    console.log(raw);
    return { ok: false, error: "Impressora não conectada", raw };
  }
  p.clear();
  p.println(raw);
  p.cut();
  await p.execute();
  return { ok: true, raw };
}
