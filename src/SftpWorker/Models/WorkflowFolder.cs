namespace SftpWorker.Models
{
    public enum WorkflowFolder
    {
        ToProcess,
        InProgress,
        Archive,
    }

    public static class WorkflowFolderUtils
    {
        public static string ToFolderName(this WorkflowFolder folder)
        {
            // If SFTP folder names match enum names, this is enough:
            return folder.ToString();

            // If not, use a switch for custom names:
            // return folder switch
            // {
            //     WorkflowFolder.ToProcess => "to_process",
            //     WorkflowFolder.InProgress => "in_progress",
            //     WorkflowFolder.Archive => "archive",
            //     _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, null)
            // };
        }
    }
}
