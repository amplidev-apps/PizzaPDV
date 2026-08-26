import type { Config } from "tailwindcss";
export default {
  content: ["./src/**/*.{ts,tsx}", "./app/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: { primary: "#dc2626", secondary: "#f59e0b" }
    }
  },
  plugins: []
} satisfies Config;
