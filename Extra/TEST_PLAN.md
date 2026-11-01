# Test Plan

## Functional
- Server start/stop; client connects/disconnects.
- Two clients connect concurrently.
- HELLO/WELCOME and PING/PONG.
- Create/join room; room full and invalid room ID.
- MOVE valid/invalid, out-of-turn, occupied cell, out-of-range coordinate.
- Board updates on both clients; win/draw result.
- Chat appears only in the intended room.

## Fault handling
- Wrong IP/port, server unavailable, client closes unexpectedly, malformed JSON, oversized message, timeout.
- Confirm app remains responsive and resources are disposed.

## Load/performance
- Record actual test at 2 clients and at a second higher load (e.g. 10 clients if feasible): connection success, message loss/errors, average response time, CPU/memory if available.
- Do not claim results until measured.

## TEST CASES – CAROONLINE

Bảng Test Case chi tiết của dự án được quản lý trên Google Sheets.

[(https://docs.google.com/spreadsheets/d/1_aqthejD0MhhW9K-uQjqvh5SNP69YWVS/edit?usp=sharing&ouid=103710399790466703203&rtpof=true&sd=true)]
Bảng bao gồm:
- Các trường hợp kiểm thử.
- Kết quả mong đợi và kết quả thực tế.
- Trạng thái PASSED, FAILED, NOT TEST YET.
- Minh chứng kiểm thử.
