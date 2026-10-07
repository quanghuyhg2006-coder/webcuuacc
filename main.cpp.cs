#include "httplib.h"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <chrono>

int main() {
    httplib::Server server;

    // Giới hạn kích thước request, gồm cả tệp tải lên.
    server.set_payload_max_length(6 * 1024 * 1024);

    server.Get("/", [](const httplib::Request&, httplib::Response& res) {
        std::ifstream page("index.html", std::ios::binary);
        if (!page) {
            res.status = 404;
            res.set_content("Không tìm thấy index.html", "text/plain; charset=utf-8");
            return;
        }

        std::string html(
            (std::istreambuf_iterator<char>(page)),
            std::istreambuf_iterator<char>()
        );
        res.set_content(html, "text/html; charset=utf-8");
    });

    server.Post("/api/requests", [](const httplib::Request& req,
                                    httplib::Response& res) {
        auto getValue = [&](const char* key) {
            return req.has_param(key) ? req.get_param_value(key) : "";
        };

        std::cout << "\n--- Yêu cầu mới ---\n"
                  << "Server: " << getValue("server") << '\n'
                  << "Content: " << getValue("content") << '\n'
                  << "Nhân vật: " << getValue("artifactName") << '\n'
                  << "Mùa: " << getValue("season") << '\n'
                  << "Hàng chờ: " << getValue("queue") << '\n'
                  << "Ủng hộ: " << getValue("support") << '\n'
                  << "Kênh liên hệ: " << getValue("channel") << '\n'
                  << "Liên hệ: " << getValue("contact") << '\n';

        if (req.has_file("proof")) {
            const auto& file = req.get_file_value("proof");

            if (file.content.size() > 5 * 1024 * 1024) {
                res.status = 400;
                res.set_content("Tệp vượt quá giới hạn 5 MB.", "text/plain; charset=utf-8");
                return;
            }

            const bool isImage = file.content_type.rfind("image/", 0) == 0;
            const bool isPdf = file.content_type == "application/pdf";
            if (!isImage && !isPdf) {
                res.status = 400;
                res.set_content("Chỉ chấp nhận tệp ảnh hoặc PDF.", "text/plain; charset=utf-8");
                return;
            }

            std::filesystem::create_directories("private_uploads");

            const auto stamp = std::chrono::steady_clock::now()
                                   .time_since_epoch().count();
            const std::string extension = isPdf ? ".pdf" : ".img";
            const auto path = std::filesystem::path("private_uploads") /
                              ("proof_" + std::to_string(stamp) + extension);

            std::ofstream output(path, std::ios::binary);
            output.write(file.content.data(),
                         static_cast<std::streamsize>(file.content.size()));

            if (!output) {
                res.status = 500;
                res.set_content("Không lưu được minh chứng.", "text/plain; charset=utf-8");
                return;
            }

            std::cout << "Đã lưu minh chứng: " << path.string() << '\n';
        }

        res.set_content("Đã nhận yêu cầu. Cảm ơn bạn!",
                        "text/plain; charset=utf-8");
    });

    std::cout << "Mở trình duyệt tại http://localhost:8080\n";
    server.listen("0.0.0.0", 8080);
}