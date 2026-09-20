# Workspace Guidelines & Agent Rules - The Last Waypoint

This file defines mandatory workspace-level guidelines and behavioral constraints for all AI agents working in this repository.

---

## 1. Visual Quality & Collaboration Rule (Quy Tắc Thẩm Mỹ & Hỗ Trợ Thiết Kế)

> [!IMPORTANT]
> **Tuyệt Đối Không Làm Tạm Bợ Cho Xong (No Makeshift/Sloppy Visual Work)**
>
> Khi thực hiện bất kỳ nhiệm vụ nào liên quan đến **Visual** (3D modeling trong Blender, texturing, materials, shaders, lighting, UI/UX layout, căn chỉnh góc camera, bố cục xe, nhân vật hoặc môi trường):
>
> 1. **Tiêu chuẩn Thẩm mỹ:** Sản phẩm visual tạo ra phải đạt chất lượng cao, hòa hợp với phong cách nghệ thuật tổng thể (atmospheric, cinematic, nostalgic post-Soviet / coastal Vietnam vibe) của *The Last Waypoint*.
> 2. **Yêu cầu Hỗ trợ / Xác nhận Khi Cần:**
>    - Nếu nhận thấy mình **không thể tự cập nhật đẹp**, thiếu tài nguyên texture/shader phù hợp, hoặc tỷ lệ mesh không tự nhiên: **BẮT BUỘC DỪNG LẠI và yêu cầu người dùng (USER) hỗ trợ hoặc xác nhận**.
>    - **KHÔNG ĐƯỢC làm tạm bợ, chắp vá cho xong việc.**
>    - **User sẵn sàng hỗ trợ:** Người dùng có thể trực tiếp setup trong Unity, tinh chỉnh material, căn chỉnh visual, hoặc confirm phương án thiết kế.
> 3. **Quy trình khi cần hỗ trợ:**
>    - Nêu rõ vấn đề thị giác đang gặp phải (ví dụ: thiếu UV map sắc nét, tỷ lệ mesh chưa tự nhiên, shader chưa phản ánh đúng độ bóng kim loại/kính...).
>    - Đưa ra 2–3 phương án cụ thể (kèm ưu/nhược điểm hoặc ảnh chụp/mô tả).
>    - Xin ý kiến hoặc nhờ User hỗ trợ setup trực tiếp trong Unity trước khi hoàn tất task.

---

## 2. Core Physics & Gameplay Protection (RCC & Malbers HAP)

- **RCC là cốt lõi của game:** Không bao giờ đưa ra thay đổi làm hỏng cấu trúc vật lý, hệ thống treo (suspension), trọng tâm (`COM`), hay các component của `RCC_CarControllerV4` và `RCC_WheelCollider`.
- **Zero-Child-Collider:** Visual mesh con dưới thân xe tuyệt đối không được có collider để bảo vệ PhysX compound collider.
- **Malbers Animations HAP:** Tuân thủ kiến trúc character controller, IK look-at và camera Cinemachine 3. Không gán đè collider làm hỏng trạng thái di chuyển của nhân vật.
