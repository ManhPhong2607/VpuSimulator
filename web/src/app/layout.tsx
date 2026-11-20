import "./globals.css";
import { Providers } from "@/components/providers";

export const metadata = { title: "VPU Simulator Console" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi">
      <body className="bg-background text-foreground text-[13px] antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}