using System;
using System.IO;
using Arise.FileSyncer.Core.FileSync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arise.FileSyncer.Core.Test
{
    [TestClass]
    public class UtilityTest
    {
        private readonly string Root = Directory.GetCurrentDirectory();

        [TestMethod]
        public void TestFileCreate()
        {
            var filePath = Path.Combine(Root, "create.test");

            if (File.Exists(filePath)) File.Delete(filePath);

            Assert.IsFalse(File.Exists(filePath));
            Assert.IsTrue(Utility.FileCreate(Guid.Empty, Root, "create.test"));
            Assert.IsTrue(File.Exists(filePath));
        }

        [TestMethod]
        public void TestFileDelete()
        {
            var filePath = Path.Combine(Root, "delete.test");

            if (File.Exists(filePath)) File.Delete(filePath);
            File.Create(filePath).Dispose();

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsTrue(Utility.FileDelete(Guid.Empty, Root, "delete.test"));
            Assert.IsFalse(File.Exists(filePath));
        }

        [TestMethod]
        public void TestFileRename_Success()
        {
            var sourcePath = Path.Combine(Root, "move s source.test");
            var targetPath = Path.Combine(Root, "move s target.test");

            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Create(sourcePath).Dispose();

            Assert.IsTrue(File.Exists(sourcePath));
            Assert.IsFalse(File.Exists(targetPath));
            Assert.IsTrue(Utility.FileRename(Guid.Empty, Root, "move s source.test", "move s target.test"));
            Assert.IsFalse(File.Exists(sourcePath));
            Assert.IsTrue(File.Exists(targetPath));
        }

        [TestMethod]
        public void TestFileRename_Fail()
        {
            var sourcePath = Path.Combine(Root, "move f source.test");
            var targetPath = Path.Combine(Root, "move f target.test");

            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Create(sourcePath).Dispose();
            File.Create(targetPath).Dispose();

            Assert.IsTrue(File.Exists(sourcePath));
            Assert.IsTrue(File.Exists(targetPath));
            Assert.IsFalse(Utility.FileRename(Guid.Empty, Root, "move f source.test", "move f target.test"));
        }

        [TestMethod]
        public void TestFileSetTime()
        {
            var filePath = Path.Combine(Root, "set time.test");
            var targetDate = new DateTime(1990, 7, 21, 14, 55, 18);

            if (File.Exists(filePath)) File.Delete(filePath);
            File.Create(filePath).Dispose();
            File.SetCreationTime(filePath, DateTime.Now);
            File.SetLastWriteTime(filePath, DateTime.Now);

            FileInfo fileInfo = new FileInfo(filePath);
            fileInfo.Refresh();

            Assert.AreNotEqual(targetDate, fileInfo.CreationTime);
            Assert.AreNotEqual(targetDate, fileInfo.LastWriteTime);
            Assert.IsTrue(Utility.FileSetTime(Guid.Empty, Root, "set time.test", targetDate, targetDate));

            fileInfo.Refresh();
            Assert.AreEqual(targetDate, fileInfo.CreationTime);
            Assert.AreEqual(targetDate, fileInfo.LastWriteTime);
        }

        [TestMethod]
        public void TestDirectoryCreate()
        {
            var directoryPath = Path.Combine(Root, "createtest");

            if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath);

            Assert.IsFalse(Directory.Exists(directoryPath));
            Assert.IsTrue(Utility.DirectoryCreate(Guid.Empty, Root, "createtest"));
            Assert.IsTrue(Directory.Exists(directoryPath));
        }
    }
}
