// electron/main.js — Janela Central + IPC impressão JP-58H
import { app, BrowserWindow, ipcMain } from "electron";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      nodeIntegration: false,
      contextIsolation: true,
    },
    title: "PizzaPDV Central — Caixa & Pedidos",
  });
  // dev: Vite, prod: dist
  const devUrl = process.env.VITE_DEV_SERVER_URL || "http://localhost:5173";
  if (!app.isPackaged) win.loadURL(devUrl);
  else win.loadFile(path.join(__dirname, "../dist/index.html"));
}

app.whenReady().then(createWindow);
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });

// IPC impressão — renderer chama window.pizzaPDV.print({type, payload})
ipcMain.handle("print", async (_event, { template, data }) => {
  // Dynamic import para não quebrar em dev sem impressora
  const { printTemplate } = await import("../src/printer/service.js");
  return printTemplate(template, data);
});

ipcMain.handle("get-status", async () => ({ online: true, printer: "JP-58H" }));
