import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  metadataBase: new URL("https://your-cat.example"),
  title: "你的猫 · 3D 桌宠",
  description: "上传多角度照片，把你家的猫变成保留花纹与体态的互动 3D 桌宠。",
  openGraph: {
    title: "把你家的猫，带到桌面上",
    description: "多角度照片 · 专属 3D 桌宠",
    images: ["/og.png"],
  },
  twitter: {
    card: "summary_large_image",
    title: "把你家的猫，带到桌面上",
    description: "多角度照片 · 专属 3D 桌宠",
    images: ["/og.png"],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
      </body>
    </html>
  );
}
