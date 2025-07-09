namespace SftpWorker.Models
{
    public class MoveFilesResult
    {
        public Dictionary<string, string> MovedFiles { get; set; }
        public Dictionary<string, Exception> FailedFiles { get; set; }

        public MoveFilesResult()
        {
            MovedFiles = new Dictionary<string, string>();
            FailedFiles = new Dictionary<string, Exception>();
        }
    }
}
