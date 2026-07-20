import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // vite.config.ts 运行在 Node 里，.env 不会自动进入 process.env，需要用 loadEnv 读取
  const env = loadEnv(mode, process.cwd(), '')
  return {
    plugins: [react(), tailwindcss()],
    server: {
      proxy: {
        // 默认代理到本地 dotnet run 的 http 端口；API 跑在 Docker（8080）时用 VITE_API_PROXY_TARGET 覆盖
        '/api': env.VITE_API_PROXY_TARGET ?? 'http://localhost:5108',
      },
    },
  }
})
