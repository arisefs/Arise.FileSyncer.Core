using System;
using System.IO;
using Arise.FileSyncer.Serializer;

namespace Arise.FileSyncer.Core.FileSync
{
    public struct FileSystemItem : IBinarySerializable
    {
        public bool IsDirectory;
        public string RelativePath;
        public long FileSize;
        public DateTime LastWriteTime;

        public FileSystemItem(bool isDirectory, string relativePath, long fileSize, DateTime lastWriteTime)
        {
            IsDirectory = isDirectory;
            RelativePath = relativePath;
            FileSize = fileSize;
            LastWriteTime = lastWriteTime;
        }

        public void Deserialize(Stream stream)
        {
            IsDirectory = stream.ReadBoolean();
            RelativePath = stream.ReadString();

            if (!IsDirectory)
            {
                FileSize = stream.ReadInt64();
                LastWriteTime = stream.ReadDateTime();
            }
            else
            {
                FileSize = 0;
                LastWriteTime = new DateTime();
            }
        }

        public void Serialize(Stream stream)
        {
            stream.WriteAFS(IsDirectory);
            stream.WriteAFS(RelativePath);

            if (!IsDirectory)
            {
                stream.WriteAFS(FileSize);
                stream.WriteAFS(LastWriteTime);
            }
        }
    }
}
