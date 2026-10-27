# Caro Online — TCP Client/Server

## Mục tiêu
Game Caro trực tuyến dạng desktop, dùng C# WinForms, TCP Socket và JSON. Server xác thực nước đi và đồng bộ trạng thái trận đấu.

## Cấu trúc
- `Code/Shared`: giao thức và luật/trạng thái dùng chung.
- `Code/Server`: TCP server, client sessions, phòng chơi, game/chat, validation và logging.
- `Code/Client`: giao diện WinForms, TCP client, view models và services.
- `Code/Tests`: kiểm thử giao thức, kết nối, luật chơi và luồng trận đấu.
- `DOCX`: báo cáo Word.
- `PPTX`: slide thuyết trình.
- `Extra`: test plan, phân công, log mẫu và link video demo.

## Khởi chạy (sau khi hoàn thành code)
1. Mở `CaroOnline.sln` bằng Visual Studio 2022 trở lên có workload .NET Desktop Development.
2. Chọn `Caro.Server` làm startup project để chạy server.
3. Chạy nhiều instance `Caro.Client` (hoặc chạy trên máy khác cùng mạng).
4. Nhập IP và port do server hiển thị/cấu hình.

> Đây là khung tổ chức và phân công. Các file có TODO cần được triển khai, tích hợp và kiểm thử trước khi dùng demo.

## Giao thức dự kiến
Mỗi message JSON có `type`, `requestId` và `payload`. Để tránh TCP chia/gộp gói, triển khai framing rõ ràng: mỗi message JSON kết thúc bằng newline (`\n`) hoặc dùng length-prefix; cả nhóm phải thống nhất một cách và dùng nhất quán. Khung này chọn newline-delimited JSON.

## Quy tắc nhóm
- Mỗi thành viên làm branch riêng, commit bằng tài khoản GitHub cá nhân.
- Không commit `bin/`, `obj/`, mật khẩu, token hoặc dữ liệu nhạy cảm.
- Không để UI thread chờ thao tác mạng; dùng async/await.
- Server là nguồn xác thực cuối cùng cho nước đi, lượt đánh và trạng thái trận đấu.
