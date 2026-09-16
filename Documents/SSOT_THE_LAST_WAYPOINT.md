# PROJECT: THE LAST WAYPOINT (TẦN SỐ CUỐI CÙNG)

# SINGLE SOURCE OF TRUTH (SSOT) — GAME DESIGN DOCUMENT

Version: 0.6 (Micro-Town Exploration, Hybrid RCC/TPS Gameplay & 3-Generation Climax)  
Stage: Early Pre-production  
Project Type: Solo-developed Hybrid Driving & Third-Person Narrative Adventure  
Target Playtime: 3.5 – 5.5 Hours (Vượt an toàn mốc 2h Steam Refund & Chuẩn thương mại)  
Primary Language: English (Full Voice Acting & Text)  
Localization: Tiếng Việt (Full Interface & Subtitles) + Đa ngôn ngữ toàn cầu  
Setting: Fictional Rainy Coastal Micro-Town (Thị trấn duyên hải mưa đêm phi biên giới)  
Working Title: The Last Waypoint (Tọa Độ Cuối Cùng / Di Sản Trên Táp-lô Cũ)  

============================================================
# 0. QUY ƯỚC TRẠNG THÁI
============================================================

[LOCKED]
Định hướng nền tảng đã được chốt. Chỉ thay đổi khi có lý do thiết kế hoặc kỹ thuật bất khả kháng.

[DRAFT]
Giả thuyết thiết kế đang được ưu tiên xây dựng. Có thể tinh chỉnh sau khi làm prototype.

[OPEN]
Vấn đề mở đang chờ quyết định cụ thể trong quá trình playtest.

[REJECTED]
Ý tưởng đã phân tích và bị loại bỏ (nhằm tránh phình to scope hoặc làm loãng câu chuyện).

============================================================
# 1. PROJECT VISION & HIGH CONCEPT
============================================================

Status: [LOCKED]

"The Last Waypoint" là một tựa game phiêu lưu tự sự kết hợp độc đáo giữa **Lái xe cơ học góc nhìn thứ nhất (First-Person Cockpit Driving)** và **Đi bộ khám phá góc nhìn thứ ba (Third-Person Narrative Exploration)**.

Người chơi vào vai một kỹ sư HMI ô tô hiện đại, trở về thị trấn ven biển nơi người cha quá cố từng sống những năm tháng cuối đời sau gần 20 năm gia đình ly tán. Di vật duy nhất bố để lại là một **chiếc xe "đồng nát" chắp vá** mua từ bãi phế liệu, được trang bị màn hình LCD tự chế gắn keo nến thô sơ.

Thay vì chỉ ngồi im trong xe suốt hành trình, người chơi sẽ:
- Lái chiếc xe cà tàng với cơ chế vật lý chân thực (Realistic Car Controller).
- Mở cửa bước xuống xe (TPS) để đi dạo trong màn mưa đêm, bước vào căn garage cũ, bãi phế liệu, quán ăn ven đường, và tiệm cầm đồ.
- Trò chuyện với những cư dân địa phương từng quen biết người cha để ghép lại bức tranh thật sự về ông: một người đàn ông từng lầm lỡ, nghèo khó, nhưng đã tằn tiện từng đồng phụ hồ để nuôi con học đại học và cố đóng một chiếc xe an toàn cho con cháu.

**CÚ TWIST KẾT GAME (THE FATHERHOOD PAYOFF):**
Khi bình minh lên ở ngọn hải đăng cuối cùng, người con nhìn qua gương chiếu hậu xuống hàng ghế sau: **Đứa con nhỏ của anh đang ngủ say sưa dưới lớp chăn ấm.** Toàn bộ chuyến đi là cuộc giải độc tâm lý để người con vượt qua nỗi sợ lặp lại sai lầm của cha mình. Màn hình HMI tự chế hiện lệnh: `VUI LÒNG NHẬP ĐÍCH ĐẾN MỚI: [ ... ]` — mở ra hành trình làm cha của chính anh.

============================================================
# 2. CENTRAL THEME & NARRATIVE DESIGN
============================================================

Status: [LOCKED]

Theme chính:
"Ta chỉ thực sự hiểu được nỗi nhọc nhằn và sự bất toàn của cha mẹ khi chính mình bước vào hành trình làm cha. Sự tha thứ không phải là xóa đi vết thương quá khứ, mà là học cách yêu thương để không lặp lại bi kịch lên thế hệ mai sau."

Lớp nghĩa bổ sung:
- **Góc nhìn đa chiều (Multi-Perspective Truth):** Người con không chỉ nghe bố tự bạch qua băng ghi âm, mà nhìn thấy cuộc đời bố qua lăng kính của những người xung quanh (ông chủ bãi xe, bà chủ quán ăn, người chủ nợ).
- **Phá vỡ lời nguyền thế hệ (Breaking the Generational Cycle):** Chuyến đi giúp người con rũ bỏ nỗi oán hận và nỗi sợ làm cha, nhận ra chiếc xe cà tàng này chính là tấm khiên che chở cho cả 3 thế hệ.

============================================================
# 3. EMOTIONAL FANTASY & PACING ARC
============================================================

Status: [LOCKED]

Fantasy cảm xúc:
"Tôi muốn được lái một chiếc xe cũ ấm cúng qua thị trấn mưa đêm, bước xuống trò chuyện với những con người bình dị, hóa giải oán hận 20 năm với người cha nghèo khó, và tìm thấy sự tự tin để trở thành một người cha tốt cho con mình."

Hành trình cảm xúc (Emotional Arc):
Xa cách / Khinh miệt (Nhìn đống sắt vụn và căn garage ẩm mốc)
↓
Tò mò / Bất ngờ (Lái xe ra phố đêm, gặp ông chủ bãi xe kể về bố)
↓
Nghẹn ngào / Xót xa (Vào quán ăn, đọc mẩu sổ nợ và chuyện bố nhịn ăn sáng)
↓
Bùi ngùi / Trân trọng (Đến tiệm cầm đồ, tìm thấy xấp biên lai học phí đại học)
↓
Vỡ òa / Tiếp nhận thiên chức (Nhìn đứa con ngủ say ở ghế sau khi bình minh lên)
↓
Vững vàng / Thanh thản (Tự tay gõ địa chỉ nhà trên HMI và khởi hành)

============================================================
# 4. BỐI CẢNH THỊ TRẤN VI MÔ (THE MICRO-TOWN HUBS)
============================================================

Status: [LOCKED]

Để đảm bảo quy mô khả thi cho Solo-Developer (10–12 tháng), thế giới game được thiết kế dạng **"Trục đường ven biển 3–5 km kết nối 4 Địa Điểm Dừng Chân" (Hub-and-Spoke)**, không làm Open-world tự do:

```
[GARAGE CỦA BỐ] ──(Lái xe)──► [BÃI PHẾ LIỆU] ──(Lái xe)──► [QUÁN ĂN ĐÊM]
      ▲                                                           │
      │                                                           ▼
(Trở về nâng cấp) ◄───────── (Lái xe) ─────────── [TIỆM CẦM ĐỒ CŨ]
                                                                  │
                                                          (Chuyến đi cuối)
                                                                  ▼
                                                      [NGỌN HẢI ĐĂNG BIỂN]
```

1. **Garage Cũ Của Bố (Home Base / Workshop):**
   - Không gian trung tâm: Bàn làm việc đầy dầu mỡ, máy hàn, đài cassette cũ, dây điện treo lủng lẳng. Nơi người chơi đỗ xe vào để tháo lắp bo mạch HMI và kiểm tra xe.
2. **Bãi Phế Liệu (The Junkyard - Gặp Bác Ba):**
   - Đống xác xe cũ rỉ sét dưới mưa. Nơi bố ngày xưa hay đến nhặt nhạnh linh kiện điện tử. Người chơi xuống xe tìm bo mạch và nghe bác Ba kể chuyện.
3. **Quán Ăn Ven Đường (The Roadside Diner - Gặp Cô Lan):**
   - Ánh đèn vàng ấm áp, tiếng quạt trần quay đều. Nơi bố hay ngồi góc khuất ăn bánh mì không. Nơi hé lộ mẩu nhật ký càu nhàu chuyện 1.5 triệu tiền ăn sáng.
4. **Tiệm Cầm Đồ Cũ (The Pawn Shop - Gặp Chú Hùng):**
   - Cửa tiệm cũ kỹ đầy đồng hồ và radio cổ. Nơi bố từng cầm cố đồ đạc để gom đủ tiền học phí đại học kỳ cuối cho con.
5. **Ngọn Hải Đăng Ven Biển (The Coastal Lighthouse - Điểm Đến Cuối Cùng):**
   - Mỏm đá nhìn ra biển khơi, nơi đón ánh bình minh rực rỡ và diễn ra cú twist 3 thế hệ.

============================================================
# 5. CƠ CHẾ GAMEPLAY KẾT HỢP (HYBRID GAMEPLAY LOOP)
============================================================

Status: [LOCKED]

5.1 Hai Chế Độ Điều Khiển (Dual Controller System):
1. **Chế độ Lái xe (In-Car / First-Person với Realistic Car Controller):**
   - Vật lý xe cà tàng rung lắc, sang số, đạp ga/phanh, bật gạt nước mưa, bật đèn pha.
   - Thao tác trực tiếp trên màn hình LCD tự chế: Quét mã lỗi DTC, xoay núm dò sóng radio FM, kiểm tra bản đồ.
2. **Chế độ Đi bộ Khám phá (On-Foot / Third-Person với Cinemachine & Mixamo):**
   - Bấm nút `[E]` hoặc `[Tam Giác / Y]` để mở cửa bước xuống xe mượt mà.
   - Đi dạo trong mưa, bước vào các cửa tiệm, nhặt linh kiện, trò chuyện với NPC.

5.2 Vòng lặp Gameplay Cốt lõi (The Town Exploration Loop):
```
[Lái xe trên đường phố đêm] 
         │
         ▼
[Tấp xe vào lề địa điểm mới] ──► [Bước xuống xe góc nhìn TPS]
                                              │
                                              ▼
                                 [Trò chuyện NPC & Tìm kỷ vật]
                                              │
                                              ▼
[Trở lại xe, sửa mạch HMI] ◄── [Nhặt được linh kiện thay thế]
         │
         ▼
[Mở khóa tọa độ tiếp theo trên bản đồ]
```

5.3 Cơ chế Cài cắm Chi tiết Ẩn (Foreshadowing Loop):
- Tiếng thở đều của đứa trẻ khi tắt radio trong xe.
- Màn hình HMI tự động kích hoạt `CABIN_STABILIZER (BABY_MODE): ON`.
- Một chiếc tất trẻ con hoặc bình sữa mini rơi ở sàn xe cạnh chiếc bút máy cũ của bố.
- Gương chiếu hậu phản chiếu góc chăn trẻ em đắp ở ghế sau.

============================================================
# 6. CẤU TRÚC HÀNH TRÌNH 4 CHƯƠNG (3.5 – 5.5 GIỜ CHƠI)
============================================================

Status: [LOCKED]

------------------------------------------------------------
CHAPTER 1: GARAGE CỦA BỐ & KHỞI ĐỘNG CỖ MÁY CŨ (~45 Phút)
------------------------------------------------------------
- Bối cảnh: Chiều muộn mưa phùn, người con bước vào căn garage hoang tàn của bố.
- Gameplay: Đi lại trong garage (TPS), khám phá bàn làm việc của bố. Mở cửa bước vào xe, đề nổ máy cà tàng, khởi động màn hình HMI dán keo nến.
- Sự kiện: Dòng code đầu tiên của bố: *"Chào con. Xe này bố tự đóng, chạy tốt đấy..."*
- Nhiệm vụ: Lái xe một vòng quanh khu phố để sạc bình ắc-quy và làm quen xe.

------------------------------------------------------------
CHAPTER 2: BÃI PHẾ LIỆU & QUÁN ĂN ĐÊM (~75 Phút)
------------------------------------------------------------
- Bối cảnh: Đường ven biển đêm sương mù dày đặc. Xe báo lỗi mất tín hiệu HMI.
- Gameplay:
  - Lái đến Bãi Phế Liệu: Xuống xe gặp Bác Ba, tìm bo mạch thay thế trong xác xe cũ.
  - Lái đến Quán Ăn Đêm: Xuống xe vào quán nước ấm cúng, gặp Cô Lan.
- Sự kiện: 
  - Nghe kể về thời trẻ bố từng nông nổi, ngoại tình, phá sản và sự hối hận muộn màng.
  - Tìm thấy trang sổ tay bố cằn nhằn chuyện 1.5 triệu tiền ăn sáng thời dự bị đại học. Lời xin lỗi nghẹn ngào của bố qua file cassette.
- ★ **CỘT MỐC 2 GIỜ NẰM TẠI ĐÂY — VƯỢT HOÀN TOÀN CHÍNH SÁCH REFUND STEAM.**

------------------------------------------------------------
CHAPTER 3: TIỆM CẦM ĐỒ & BÃO ĐÊM TRÊN ĐÈO (~75 Phút)
------------------------------------------------------------
- Bối cảnh: Đêm bão giông dữ dội, sấm chớp rạch trời trên cung đường đèo ven biển.
- Gameplay:
  - Ghé Tiệm Cầm Đồ Cũ: Xuống xe gặp Chú Hùng, chuộc lại chiếc bút máy kỷ niệm ngày đỗ đại học.
  - Phát hiện folder `HOC_PHI_DAI_HOC`: Toàn bộ biên lai 4 năm đại học và sổ ghi nợ từng đồng của bố.
  - Lái xe vượt bão: Động cơ quá nhiệt, hệ thống điện chập chờn, người chơi vừa lái vừa xử lý cầu chì HMI.
- Cảm xúc: Cao trào kịch tính, bàng hoàng, thấu hiểu sự hy sinh thô ráp của bố.

------------------------------------------------------------
CHAPTER 4: NGỌN HẢI ĐĂNG & THIÊN CHỨC LÀM CHA (~45 Phút)
------------------------------------------------------------
- Bối cảnh: 05:30 sáng, bão tan, bình minh vàng ươm rọi xuống mỏm đá ngọn hải đăng.
- Gameplay: Xe từ từ dừng lại ngắm biển. Không còn câu đố căng thẳng.
- Sự kiện & Cú Twist:
  - Đặt chiếc bút máy của bố lên táp-lô.
  - Camera xoay ra sau: Người con nhìn đứa bé đang ngủ say hé mở mắt mỉm cười.
  - Màn hình HMI tự chế hiện dòng chữ:
    `HANH TRINH CUA BO: HOAN TAT.`
    `VUI LONG NHAP DICH DEN MOI: [ _____________ ]`
  - Người con tự tay gõ vào: *"Về Nhà"*. Bật bài hát radio bố thích nhất và khởi hành chuyến đi mới.

============================================================
# 7. TECH STACK & GIẢI PHÁP ĐỒ HỌA/NPC CHO SOLO-DEV
============================================================

Status: [LOCKED]

7.1 Bộ Công Nghệ Cốt Lõi:
- **Engine:** Unity (URP) — Tối ưu hóa ánh sáng đêm mưa và tích hợp UI HMI mượt mà.
- **Driving Physics:** Realistic Car Controller (RCC) — Gói điều khiển xe vật lý chuẩn công nghiệp, có sẵn cơ chế Enter/Exit xe.
- **Character & Camera:** Unity Starter Assets / Invector TPS Controller + Cinemachine (Camera xoay mượt mà).
- **Animations:** 100% tận dụng thư viện miễn phí Adobe Mixamo (Đi, chạy, đứng chờ, gật đầu, khoanh tay trò chuyện).

7.2 Giải pháp Khuôn mặt & Hội thoại NPC (Né bẫy đồ họa):
- **Góc máy Điện ảnh (Cinematic Over-the-shoulder / Medium Shot):** Không quay cận cảnh khuôn mặt. Tập trung vào cử chỉ hình thể (gật đầu, nhún vai, châm thuốc).
- **Audio-based Lip-Flap:** Dùng tool tự động mở khẩu hình miệng đơn giản theo âm lượng giọng nói (như OVRLipSync / Salsa miễn phí).
- **Lồng tiếng tiếng Anh chuyên nghiệp (English Voice Acting):** Thuê qua Fiverr/Voice123 (150$–300$ cho 4 nhân vật), giọng diễn viên truyền cảm gánh 80% cảm xúc.

============================================================
# 8. SCOPE PRINCIPLES & SOLO-DEV ROADMAP (10–12 THÁNG)
============================================================

Status: [LOCKED]

Cam kết kiểm soát Scope để dự án chắc chắn phát hành thành công:
1. **Micro-Town khép kín:** 1 trục đường ven biển nối 4 địa điểm, không làm thành phố tự do.
2. **Nội thất tối giản:** Các cửa tiệm (Quán ăn, tiệm cầm đồ) chỉ dựng 1 góc phòng ấm cúng nơi NPC đứng sau quầy, không làm cả tòa nhà lớn.
3. **Số lượng NPC tối giản:** Đúng 3 NPC phụ + 1 giọng nói của người cha.

Timeline 12 tháng:
- **Tháng 1-2: Core Prototype:** Tích hợp RCC + TPS Controller + Cơ chế Lên/Xuống xe + Màn hình HMI 3D Canvas.
- **Tháng 3-4: Dựng Cung đường Micro-Town & Garage:** Ghép trục đường ven biển 3km và hoàn thiện Chapter 1 (Vertical Slice).
- **Tháng 5-7: Production Chapter 2 & 3:** Dựng Bãi phế liệu, Quán ăn, Tiệm cầm đồ, lập trình hệ thống hội thoại NPC.
- **Tháng 8-9: Polish Weather Shaders & Voice Acting:** Hoàn thiện cảnh bão đèo dốc, cảnh bình minh hải đăng, thu âm lồng tiếng tiếng Anh.
- **Tháng 10: Steam Page & Demo Next Fest:** Mở trang Steam, tung bản demo chơi thử Chương 1.
- **Tháng 11-12: Bug fix, bản địa hóa tiếng Việt hoàn chỉnh & Release chính thức.**

============================================================
# 9. ONE-SENTENCE SUMMARY
============================================================

"The Last Waypoint là một tựa game phiêu lưu tự sự kết hợp lái xe và khám phá thị trấn, nơi một kỹ sư HMI sửa chữa chiếc xe phế liệu của người cha quá cố qua lời kể của cư dân địa phương, trước khi vỡ òa nhận ra đứa con nhỏ đang ngủ ở ghế sau — đưa anh vào hành trình học cách làm cha của chính mình."
