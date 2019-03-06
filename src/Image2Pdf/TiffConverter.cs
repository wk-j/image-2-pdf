using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image2Pdf {

    public class TiffConverter {

        private readonly CommandProcessor _processor;
        private readonly PathService _pathService;

        public TiffConverter(PathService pathService, CommandProcessor processor) {
            _processor = processor;
            _pathService = pathService;
        }

        private IEnumerable<string> Quote(IEnumerable<string> input) =>
            input.Select(d => $@"""{d}""").ToList();

        public CommandResult ProcessDensity(List<string> _inputs, String target, int dpi, bool compress) {
            var convert = _pathService.GetConvertPath();
            var compressFlag = compress ? "+compress" : "";

            var inputs = String.Join(" ", Quote(_inputs));
            var command = $@"-density {dpi} -units PixelsPerInch {compressFlag} -adjoin {inputs} ""{target}""";
            var workingDir = ".";

            if (command.Length > CommandProcessor.MaxCommandLength) {
                var newInputs = _inputs.Select(x => new FileInfo(x).Name);
                workingDir = new FileInfo(_inputs[0]).Directory.FullName;
                inputs = String.Join(" ", Quote(newInputs));
                command = $@"-density {dpi} -units PixelsPerInch {compressFlag} -adjoin {inputs} ""{target}""";
            }

            var fullCommand = String.Format("{0} {1}", convert, command);
            var result = _processor.Process(convert, command, workingDir);

            if (!result.Success) {
                return result;

            } else {
                var file = new FileInfo(target);

                if (result.Error.Length > 0) {
                    return new CommandResult { Success = false };
                }

                if (!file.Exists) {
                    return new CommandResult { Success = false };
                }

                if (file.Length == 0) {
                    return new CommandResult { Success = false };
                }
            }
            return new CommandResult { Success = true };
        }
    }
}