namespace Mailr.Extensions.Models
{
    public class Footer
    {
        public string ProgramName { get; set; } = ProgramInfo.Name;

        public string ProgramVersion { get; set; } = ProgramInfo.Version;
    }
}