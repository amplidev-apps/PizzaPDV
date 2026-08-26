import { contextBridge, ipcRenderer } from "electron";
contextBridge.exposeInMainWorld("pizzaPDV", {
  print: (args) => ipcRenderer.invoke("print", args),
  getStatus: () => ipcRenderer.invoke("get-status"),
});
