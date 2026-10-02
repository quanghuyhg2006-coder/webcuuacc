#include "httplib.h"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>

int main() {
    const std::filesystem::path dataDir = R"(D:\web_cv)";
    const auto uploadsDir = dataDir / "private_uploads";

    try {
        std::filesystem::create_directories(uploadsDir);
    } catch (const std::exception& e) {
        std::cerr << "Không tạo được thư mục D:\\web_cv: " << e.what() << '\n';
        return 1;
    }

    httplib::Server server;
    server.set_payload_max_length(6 * 1024 * 1024);

    server.Get("/", [](const httplib::Request&, httplib::Response& res) {
        std::ifstream page("index.html", std::ios::binary);
        if (!page) {
            res.status = 404;
            res.set_content("Không tìm thấy index.html", "text/plain; charset=utf-8");
            return;
        }

        std::string html((std::istreambuf_iterator<char>(page)),
                         std::istreambuf_iterator<char>());
        res.set_content(html, "text/html; charset=utf-8");
    });

    server.Post("/api/requests", [dataDir, uploadsDir](
        const httplib::Request& req, httplib::Response& res) {
        auto getValue = [&](const char* key) {
            return req.has_param(key) ? req.get_param_value(key) : "";
        };

        const auto id = std::chrono::steady_clock::now()
                            .time_since_epoch().count();

        std::string proofPath;
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

            const auto path = uploadsDir /
                ("proof_" + std::to_string(id) + (isPdf ? ".pdf" : ".img"));

            std::ofstream output(path, std::ios::binary);
            output.write(file.content.data(),
                         static_cast<std::streamsize>(file.content.size()));

            if (!output) {
                res.status = 500;
                res.set_content("Không lưu được minh chứng.", "text/plain; charset=utf-8");
                return;
            }
            proofPath = path.string();
        }

        std::ofstream log(dataDir / "requests.txt", std::ios::app);
        if (!log) {
            res.status = 500;
            res.set_content("Không lưu được yêu cầu vào ổ D.", "text/plain; charset=utf-8");
            return;
        }

        log << "--- Yêu cầu mới ---\n"
            << "Server: " << getValue("server") << '\n'
            << "Content: " << getValue("content") << '\n'
            << "Nhân vật: " << getValue("artifactName") << '\n'
            << "Mùa: " << getValue("season") << '\n'
            << "Hàng chờ: " << getValue("queue") << '\n'
            << "Ủng hộ: " << getValue("support") << '\n'
            << "Kênh liên hệ: " << getValue("channel") << '\n'
            << "Liên hệ: " << getValue("contact") << '\n'
            << "Minh chứng: " << proofPath << "\n\n";

        res.set_content("Đã nhận yêu cầu. Cảm ơn bạn!",
                        "text/plain; charset=utf-8");
    });

    std::cout << "Trang web: http://localhost:8080\n"
              << "Dữ liệu sẽ lưu tại: D:\\web_cv\n";

    if (!server.listen("0.0.0.0", 8080)) {
        std::cerr << "Không thể khởi động máy chủ tại cổng 8080.\n";
        return 1;
    }
}