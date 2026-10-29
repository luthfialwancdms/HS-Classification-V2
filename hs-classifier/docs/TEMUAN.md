# TEMUAN — pekerjaan fungsi terpisah (di luar restyle)

Dicatat selama restyle v0. **Tidak diperbaiki** di rangkaian restyle ini; menunggu keputusan sebagai pekerjaan fungsi tersendiri.

| # | Temuan | Dampak | Usul |
|---|---|---|---|
| F1 | Notice sukses "Case disetujui."/"Case ditolak." tidak sempat terlihat. `act()` memanggil `setNotice(label)` lalu `await onDone()`; `onDone` → `loadCase()` mengosongkan `detail` lebih dulu, sehingga panel keputusan unmount dan notice hilang sebelum sempat dirender. | Ahli tidak melihat konfirmasi visual setelah keputusan berhasil; satu-satunya tanda adalah daftar/case yang berubah. | Pisahkan state list/detail saat refresh, atau tampilkan notice di level halaman (bertahan lintas remount). Perubahan perilaku — butuh keputusan. |
| F2 | Syarat alasan minimal **5 karakter** (`reason.trim().length >= 5`) di panel keputusan ahli dan kotak "Minta Expert Review", sedangkan spesifikasi menyebut **10 karakter** (placeholder v0 pun menulis "minimal 10 karakter"). | Keputusan bisa disimpan dengan alasan yang terlalu pendek menurut spesifikasi; placeholder UI tidak sesuai aturan sebenarnya. | Samakan ke 10 karakter (atau perbarui spesifikasi). Menyentuh validasi = perubahan fungsi, bukan restyle. |

Catatan: F1 dan F2 berlaku juga untuk tombol "Minta Expert Review" di layar klasifikasi (komponen yang sama, ambang 5 karakter).
