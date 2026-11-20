import { redirect } from "next/navigation";
import { getSessionUser } from "@/lib/auth";
import { LoginForm } from "./login-form";

export default async function LoginPage() {
  if (await getSessionUser()) redirect("/vpu");

  return (
    <div className="flex min-h-screen items-center justify-center">
      <div className="flex w-[380px] flex-col gap-4 rounded-[10px] border bg-card p-8">
        <div className="flex items-center gap-3">
          <div className="size-[38px] rounded-[10px] bg-primary" />
          <div>
            <b className="text-[17px]">VPU Simulator</b>
            <div className="text-xs text-muted-foreground">Control console</div>
          </div>
        </div>

        <LoginForm />

        <p className="text-[11.5px] leading-normal text-[#8b8f9a]">
          Tài khoản cấu hình sẵn qua biến môi trường của Console, không lưu
          trong mã nguồn. Đổi khi triển khai thật.
        </p>
      </div>
    </div>
  );
}