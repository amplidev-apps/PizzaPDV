/** @type {import('next').NextConfig} */
const nextConfig = {
  transpilePackages: ["@pizzapdv/shared"],
  experimental: { typedRoutes: true }
};
export default nextConfig;
