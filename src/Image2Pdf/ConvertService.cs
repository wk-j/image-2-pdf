using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image2Pdf {

    public class ConvertService {
        private readonly ILogger<ConvertService> _logger;
        private Quality _quality;
        private String _finalTifFile = null;
        private PathService _pathService;
        private TiffConverter _tiff;

        public ConvertService(ILogger<ConvertService> logger, Quality quality, PathService pathService, TiffConverter tiff) {
            _logger = logger;
            _quality = quality;
            _tiff = tiff;
            _pathService = pathService;
        }

        public String GetFinalTifFile() {
            return _finalTifFile;
        }

        public async Task<List<String>> CompressImagesAsync(List<String> sources) {
            var convert = _pathService.GetConvertPath();
            var max = 20;
            var size = sources.Count();
            var results = new List<string>();
            var skip = 0;

            while (skip < size) {
                var partailSource = sources.Skip(skip).Take(max);
                var compressTask = CreateTask(partailSource, convert);
                var rs = await Task.WhenAll(compressTask);
                results.AddRange(rs);
                skip = skip + max;
            }

            return results.ToList();
        }

        private List<Task<string>> CreateTask(IEnumerable<string> sources, string convert) {
            var tasks = new List<Task<string>>();
            foreach (var i in Enumerable.Range(0, sources.Count())) {
                var source = sources.ElementAt(i);
                var task = CompressAsync(source, convert);
                tasks.Add(task);
            }
            return tasks;
        }

        public int GetUniqueColors(String imagePath) {
            var identify = _pathService.GetIdentifyPath();
            var args = String.Format(" -format %k {0}", _pathService.Quote(imagePath));

            var start = new ProcessStartInfo {
                FileName = identify,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            _logger.LogInformation("identify path - {0}", identify);

            var sb = new StringBuilder();
            var proccess = new Process();
            proccess.StartInfo = start;
            proccess.Start();

            while (!proccess.StandardOutput.EndOfStream) {
                sb.Append(proccess.StandardOutput.ReadLine());
            }

            var colors = sb.ToString();
            var numberOfColors = 2;
            Int32.TryParse(colors, out numberOfColors);

            return numberOfColors;
        }

        public Task<String> CompressAsync(String source, String convert) {
            _logger.LogInformation("compress - {0}", source);
            var sourceInfo = new FileInfo(source);

            // File compression issue
            // If Type = PNG, Color = B/W
            // - Compress with Group4 into TIF => OK
            // - Compress with Group4 into JPG => Bigger than original
            // If Type = PNG, Color = Color/Gray
            // - Compress with LZW into TIF => Bigger
            // - Compress with LZW into JPG => OK
            var sourceExtension = sourceInfo.Extension;
            var targetExtension = ".tif";

            // If size > 1 MB
            // - Convert as LZW into .JPG
            // Else
            // - Convert as Group4 into .TIF
            // Compress params
            // @type = [LZW/Group4]
            // @source = source image (.PNG)
            // @target = target image [.TIF/.JPG]
            var args = $"-compress $type -quality $quality $source $target";
            var color = GetUniqueColors(source);

            if (color > 2) {
                targetExtension = ".jpg";
                args = args.Replace("$type", "LZW");
                args = args.Replace("$quality", _quality.ColorQuality.ToString());
            } else {
                args = args.Replace("$type", "Group4");
                args = args.Replace("$quality", _quality.BlackWhiteQuality.ToString());
            }

            var targetDir = _pathService.GetCompressPath();
            var target = Path.Combine(targetDir, sourceInfo.Name).Replace(sourceExtension, targetExtension);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            Task<String> tasks = Task.Run(() => {
                var realTarget = _pathService.Quote(target);
                var info = new ProcessStartInfo {
                    FileName = convert,
                    Arguments = args
                        .Replace("$source", _pathService.Quote(source))
                        .Replace("$target", realTarget),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                var compressArgs = String.Join(" ", convert, info.Arguments);
                _logger.LogInformation("command - {0}", compressArgs);

                var process = new Process {
                    StartInfo = info
                };
                process.Start();
                process.WaitForExit();

                var targetInfo = new FileInfo(target);

                if (targetInfo.Exists && targetInfo.Length > 0) {
                    return target;
                } else {
                    return source;
                }
            });

            return tasks;
        }

        private ConvertResult CreateFinalTiffFile(List<String> compressedImage, String target) {

            var result = _tiff.ProcessDensity(compressedImage, target, 200, false);
            if (result.Success) {
                this._finalTifFile = target;
            } else {
                result = _tiff.ProcessDensity(compressedImage, target, 200, true);
                if (result.Success) {
                    this._finalTifFile = target;
                }
            }

            return new ConvertResult {
                Result = target,
                Success = result.Success,
                Message = result.Message
            };
        }

        private void DeleteFiles(List<string> sources) {
            sources.Where(File.Exists).ToList().ForEach(File.Delete);
        }

        public async Task<ConvertResult> Png2PdfAAsync(List<String> sources, String target) {
            var images = await CompressImagesAsync(sources);
            var result = this.CreateFinalTiffFile(images, target.Replace(".pdf", ".tif"));

            if (!result.Success) {
                _logger.LogError("create final tiff failed | {0}", result.Message);
                DeleteFiles(images);
                return result;
            }

            var dict = new Dictionary<String, String>();
            var compressOk = true;

            sources.ForEach(s => {
                if (images.Contains(s)) {
                    _logger.LogError("source file not exist | {0}", s);
                    compressOk = false;
                }
            });

            images.ForEach(s => {
                var info = new FileInfo(s);
                if (!info.Exists) {
                    _logger.LogError("file not exist - {0}", info.FullName);
                    compressOk = false;
                } else if (info.Length < 0.5 * 1027) {
                    _logger.LogError("too small length - {0}", info.FullName);
                    compressOk = false;
                }
            });

            if (compressOk) {
                var document = new Document();
                document.SetMargins(0, 0, 0, 0);

                var writer = PdfAWriter.GetInstance(document, new FileStream(target, FileMode.Create), PdfAConformanceLevel.PDF_A_1B);
                writer.CreateXmpMetadata();
                document.Open();

                images.ForEach(i => {
                    // http://stackoverflow.com/questions/19256275/fitting-image-into-pdf-using-itext
                    var img = Image.GetInstance(i);
                    img.SetAbsolutePosition(0f, 0f);

                    var dpiX = img.DpiX;
                    var dpiY = img.DpiY;

                    var width = PixelsToPoints(img.Width, dpiX);
                    var height = PixelsToPoints(img.Height, dpiY);

                    var sizeOk = document.SetPageSize(new Rectangle(0, 0, width, height));
                    if (sizeOk) {
                        var scaler = (document.PageSize.Width / img.Width) * 100;
                        img.ScalePercent(scaler);
                    }

                    document.NewPage();

                    // Append ocr content first.
                    if (dict.Keys.Contains(i)) {
                        var value = dict[i];
                        var text = new Chunk(value);
                        text.setLineHeight(0);
                        text.SetCharacterSpacing(0);
                        text.SetWordSpacing(0);

                        var under = writer.DirectContentUnder;
                        var cb = new PdfContentByte(writer);
                        cb.BeginText();
                        cb.SetFontAndSize(BaseFont.CreateFont(), 8f);
                        cb.ShowText(value);
                        cb.EndText();
                        under.Add(cb);
                    }

                    // Append image above of ocr content.
                    document.Add(img);
                });

                document.Close();
                writer.Close();
                DeleteFiles(images);
                return new ConvertResult {
                    Success = true,
                    Result = target,
                    Message = ""
                };
            } else {
                _logger.LogError("convert to pdf failed ...");
                DeleteFiles(images);
                return new ConvertResult {
                    Success = false,
                    Result = "",
                    Message = ""
                };
            }
        }

        public async Task<ConvertResult> Png2PdfAsync(List<String> sources, String target) {
            var images = await CompressImagesAsync(sources);
            var result = this.CreateFinalTiffFile(images, target.Replace(".pdf", ".tif"));
            var dict = new Dictionary<String, String>();
            var compressOk = sources.All(x => !images.Contains(x));

            if (!result.Success) {
                DeleteFiles(images);
                return result;
            }

            images.ForEach(s => {
                var info = new FileInfo(s);
                if (!info.Exists) {
                    compressOk = false;
                } else {
                    if (info.Length < 0.5 * 1027) compressOk = false;
                }
            });

            if (compressOk) {
                var document = new Document();
                document.SetMargins(0, 0, 0, 0);

                var writer = PdfWriter.GetInstance(document, new FileStream(target, FileMode.Create));
                writer.SetPdfVersion(PdfWriter.PDF_VERSION_1_5);
                document.Open();

                images.ForEach(i => {
                    // http://stackoverflow.com/questions/19256275/fitting-image-into-pdf-using-itext
                    var img = Image.GetInstance(i);
                    img.SetAbsolutePosition(0f, 0f);

                    var dpiX = img.DpiX == 0 ? 92 : img.DpiX;
                    var dpiY = img.DpiY == 0 ? 92 : img.DpiX;

                    var width = PixelsToPoints(img.Width, dpiX);
                    var height = PixelsToPoints(img.Height, dpiY);
                    var sizeOk = document.SetPageSize(new Rectangle(0, 0, width, height));

                    _logger.LogInformation($"convert - {width}({dpiX}) {height}({dpiY})");

                    if (sizeOk) {
                        var scaler = (document.PageSize.Width / img.Width) * 100;
                        img.ScalePercent(scaler);
                    }

                    document.NewPage();

                    // Append ocr content first.
                    if (dict.Keys.Contains(i)) {
                        var value = dict[i];
                        var text = new Chunk(value);
                        text.setLineHeight(0);
                        text.SetCharacterSpacing(0);
                        text.SetWordSpacing(0);

                        var under = writer.DirectContentUnder;
                        var cb = new PdfContentByte(writer);
                        cb.BeginText();
                        cb.SetFontAndSize(BaseFont.CreateFont(), 8f);
                        cb.ShowText(value);
                        cb.EndText();
                        under.Add(cb);
                    }
                    // Append image above of ocr content.
                    document.Add(img);
                });

                document.Close();
                writer.Close();

                DeleteFiles(images);
                return new ConvertResult {
                    Success = true,
                    Result = target,
                    Message = ""
                };
            } else {
                DeleteFiles(images);
                return new ConvertResult {
                    Success = false,
                    Result = "",
                    Message = ""
                };
            }
        }

        public static float PixelsToPoints(float value, int dpi) {
            return value / dpi * 72;
        }
    }
}