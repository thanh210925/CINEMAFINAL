document.addEventListener("DOMContentLoaded", () => {
    const movieSelect = document.getElementById("movieSelect");
    const dateSelect = document.getElementById("dateSelect");
    const showtimeSelect = document.getElementById("showtimeSelect");

    if (!movieSelect) return;

    movieSelect.addEventListener("change", async function () {
        const movieId = this.value;

        dateSelect.innerHTML = '<option selected disabled>📅 Chọn ngày</option>';
        showtimeSelect.innerHTML = '<option selected disabled>⏰ Chọn suất chiếu</option>';

        try {
            const res = await fetch(`/Home/GetShowtimesByMovie?movieId=${movieId}`);
            const data = await res.json();

            if (!data.length) {
                alert("Phim này hiện chưa có lịch chiếu!");
                return;
            }

            // Lấy ngày duy nhất
            const dates = [...new Set(data.map(s => s.date))];
            dates.forEach(d => {
                const opt = document.createElement("option");
                const parts = d.split("-");
                opt.value = d;
                opt.textContent = `${parts[2]}/${parts[1]}/${parts[0]}`;
                dateSelect.appendChild(opt);
            });

            dateSelect.onchange = () => {
                const selectedDate = dateSelect.value;
                showtimeSelect.innerHTML = '<option selected disabled>⏰ Chọn suất chiếu</option>';

                data.filter(s => s.date === selectedDate).forEach(s => {
                    const opt = document.createElement("option");
                    opt.value = s.showtimeId;
                    opt.textContent = `${s.time} - Giá: ${Number(s.price).toLocaleString("vi-VN")}đ`;
                    showtimeSelect.appendChild(opt);
                });
            };
        } catch (err) {
            console.error("❌ API Error:", err);
        }
    });
});
