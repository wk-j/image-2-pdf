using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Image2Pdf {

    public class CommandProcessor {

        public static int MaxCommandLength => 32767;
        private ILogger<CommandProcessor> _logger;

        public CommandProcessor(ILogger<CommandProcessor> logger) {
            _logger = logger;
        }

        public CommandResult Process(String command, String args, string workingDir = ".") {
            // https://support.microsoft.com/en-us/kb/830473

            _logger.LogInformation("command length - {0}", command.Length + args.Length);

            if (args.Length > MaxCommandLength) {
                _logger.LogError("command line to long ...");
                _logger.LogError("{0} {1}", command, args);

                return new CommandResult {
                    Success = false,
                    Message = "command prompt cannot contain more than either 2047 or 8191 characters."
                };
            }

            var info = new ProcessStartInfo {
                Arguments = args,
                FileName = command,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDir
            };

            var process = new Process {
                StartInfo = info
            };

            process.Start();

            var output = new StringBuilder();
            var error = new StringBuilder();

            while (!process.StandardOutput.EndOfStream) {
                var line = process.StandardOutput.ReadLine();
                output.Append(line);
            }

            while (!process.StandardError.EndOfStream) {
                var line = process.StandardError.ReadLine();
                error.Append(line);
            }

            process.WaitForExit();

            return new CommandResult {
                Success = true,
                Message = output.ToString(),
                Error = error.ToString()
            };
        }
    }
}