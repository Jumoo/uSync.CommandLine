using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uSync.Commands.Core.Commands;
public interface ISyncCommand
{
    public Command Command { get; }
}
