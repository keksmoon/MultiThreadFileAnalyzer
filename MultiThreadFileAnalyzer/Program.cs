using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MultiThreadFileAnalyzer {
    // Задание.
    //
    // В файле настроек записано имя диска/каталога, с которым будет вестись
    // работа.Первый поток просматривает исходный каталог и все вложенные
    // подкаталоги(использовать стек или очередь!!!, а не встроенные методы),
    // записывает в очередь все файлы, которые там хранятся.Второй поток
    // последовательно берет файлы из очереди, считает у каждого файла некоторый
    // код(согласно варианту), и выводит на консоль результат: имя файла – значение
    // суммы.Синхронизировать потоки, чтобы они не блокировали друг друга при
    // обращении к очереди.
    //
    // 18. Создание потоков с помощью класса Thread - 
    // подсчет произведения последних двух байтов файла и 
    // и синхронизация их с помощью Reader/WriterLock

    [Serializable]
    public class DirectoryDescription {
        public string FullName { get; set; }

        public DirectoryDescription() { }
        public DirectoryDescription(string DirFullName) => FullName = DirFullName;
    }
    class Program {
        //Очередь с файлами
        static Queue<FileInfo> fileInfos = new Queue<FileInfo>();
        static ReaderWriterLock rwl = new ReaderWriterLock();
        [STAThread]
        static void Main() {
            DirectoryDescription directoryDescription;

            #region Get directoryDescription \ Serializing
            Console.WriteLine("1. Select the saved settings file and continue working with it. \r\n" +
                "2. Select a directory and create a new settings file.");
            switch (Console.ReadLine()) {
                case "1":
                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.Filter = "XML files(*.xml)|*.xml";

                    if (openFileDialog.ShowDialog() == DialogResult.OK) {
                        //Восстанавливаем файл настроек
                        XmlSerializer xmlFormat = new XmlSerializer(typeof(DirectoryDescription));

                        Stream fStream = File.OpenRead(openFileDialog.FileName);
                        directoryDescription = (DirectoryDescription)xmlFormat.Deserialize(fStream);
                        fStream.Close();
                    }
                    else return;
                    break;
                case "2":
                    FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                    if (folderBrowserDialog.ShowDialog() == DialogResult.OK) {
                        string pathToFolder = folderBrowserDialog.SelectedPath;

                        directoryDescription = new DirectoryDescription(pathToFolder);

                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.Filter = "XML files(*.xml)|*.xml";
                        if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                            //Выполняем сохранение файла настроек
                            XmlSerializer xmlFormat = new XmlSerializer(typeof(DirectoryDescription));

                            Stream fStream = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
                            xmlFormat.Serialize(fStream, directoryDescription);
                            fStream.Close();
                        }
                    }
                    else return;
                    break;
                default:
                    return;
            }
            #endregion

            Stack<DirectoryInfo> directoriesInfo = new Stack<DirectoryInfo>(new DirectoryInfo[] { new DirectoryInfo(directoryDescription.FullName) });
            bool ReadingThreadIsAlive = true;
            Thread ReadingThread = new Thread(() => {
                while (directoriesInfo.Count != 0) {
                    DirectoryInfo directoryInfo = directoriesInfo.Pop();

                    foreach (var file in directoryInfo.EnumerateFiles()) {
                        rwl.AcquireReaderLock(Timeout.Infinite);
                        try {
                            fileInfos.Enqueue(file);
                        }
                        finally { rwl.ReleaseReaderLock(); }
                    }

                    foreach (var directory in directoryInfo.EnumerateDirectories())
                        directoriesInfo.Push(directory);
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Thread #1: I did it!");
                Console.ForegroundColor = ConsoleColor.White;
                ReadingThreadIsAlive = false;
            });

            int cntr = 0;
            Thread HandlerThread = new Thread(() => {
                while (true) {
                    if (fileInfos.Count != 0) {
                        FileStream fstream = null;
                        string fileName;

                        rwl.AcquireWriterLock(Timeout.Infinite);
                        try {
                            fstream = File.OpenRead(fileName = fileInfos.Dequeue().FullName);
                        }
                        finally { rwl.ReleaseWriterLock(); }
                        byte[] array = new byte[fstream.Length];
                        
                        fstream.Read(array, 0, array.Length);
                        fstream.Close();

                        if (array.Length < 2) 
                            Console.WriteLine($"{++cntr}. {fileName} - Файл очень короток");
                        else 
                            Console.WriteLine($"{++cntr}. {fileName} : {array[array.Length - 1] * array[array.Length - 2]}");
                    }
                    else if (!ReadingThreadIsAlive) break;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Thread #2: I did it!");
                Console.ForegroundColor = ConsoleColor.White;
            });

            ReadingThread.Start();
            HandlerThread.Start();

            Console.ReadKey();
        }
    }
}