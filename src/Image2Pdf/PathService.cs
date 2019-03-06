using System;
using System.IO;

namespace Image2Pdf {
    public class PathService {
        private string _magickPath;
        private string _tempPath;
        private string _imagePath;

        public PathService(string magickPath, string tempPath, string imagePath) {
            _magickPath = new DirectoryInfo(magickPath).FullName;
            _tempPath = new DirectoryInfo(tempPath).FullName;
            _imagePath = new DirectoryInfo(imagePath).FullName;
        }

        public String Quote(String source) =>
            $@"""{source}""";

        public string GetTempPath =>
            _tempPath;

        private string Now() =>
            DateTime.Now.ToString("yyyyMMdd");

        public string GetCompressPath() =>
            Path.Combine(_tempPath, "__compress__");

        public string GetFinalPath(string id) =>
            Path.Combine(_tempPath, "__final__", id);

        public string GetImagePath() =>
            _imagePath;

        public String GetConvertPath() {
            if (Environment.OSVersion.Platform == PlatformID.Unix) {
                return Path.Combine(_magickPath, "convert");
            }
            return Quote(Path.Combine(_magickPath, "convert.exe"));
        }

        public String GetIdentifyPath() {
            if (Environment.OSVersion.Platform == PlatformID.Unix) {
                return Path.Combine(_magickPath, "identify");
            }
            return Quote(Path.Combine(_magickPath, "identify.exe"));
        }
    }
}