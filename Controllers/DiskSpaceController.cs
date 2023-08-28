using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebDirectory.Models;

public class DiskSpaceController : Controller
{
    private const int MaxDepth = 3;

    [ResponseCache(Duration = 3600)] // Кэширование на стороне сервера на 1 час
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        string rootPath = @"C:\";
        List<FileSystemItem> items = await ProcessDirectoryAsync(new DirectoryInfo(rootPath), 0);

        int totalCount = items.Count;
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewData["TotalCount"] = totalCount;
        ViewData["TotalPages"] = totalPages;
        ViewData["CurrentPage"] = page;
        ViewData["PageSize"] = pageSize;

        return View(items);
    }

    private async Task<List<FileSystemItem>> ProcessDirectoryAsync(DirectoryInfo directoryInfo, int depth)
    {
        if (depth >= MaxDepth)
        {
            return new List<FileSystemItem>();
        }

        try
        {
            var subItems = new List<FileSystemItem>();
            var subItemTasks = new List<Task<List<FileSystemItem>>>();

            foreach (var file in directoryInfo.GetFiles())
            {
                subItems.Add(new FileSystemItem
                {
                    Name = file.Name,
                    Path = file.FullName,
                    Size = file.Length,
                    IsDirectory = false
                });
            }

            foreach (var subDirectory in directoryInfo.GetDirectories())
            {
                var subDirectoryItem = new FileSystemItem
                {
                    Name = subDirectory.Name,
                    Path = subDirectory.FullName,
                    IsDirectory = true,
                    SubItems = new List<FileSystemItem>(), // Инициализируем пустой список поддиректорий
                    Level = depth + 1
                };

                subItemTasks.Add(ProcessDirectoryAsync(subDirectory, depth + 1)); // Рекурсивно обрабатываем поддиректорию асинхронно
                subItems.Add(subDirectoryItem);
            }

            var subItemResults = await Task.WhenAll(subItemTasks); // Ожидаем завершения всех задач
            foreach (var result in subItemResults)
            {
                subItems.AddRange(result);
            }

            return subItems;
        }
        catch (UnauthorizedAccessException)
        {
            // Обработка исключения
            return new List<FileSystemItem>();
        }
    }

    private async Task<long> CalculateDirectorySizeAsync(DirectoryInfo directoryInfo)
    {
        long size = 0;
        try
        {
            var fileTasks = directoryInfo.GetFiles().Select(file => Task.Run(() => file.Length));
            size += (await Task.WhenAll(fileTasks)).Sum();

            var subDirectoryTasks = directoryInfo.GetDirectories().Select(subDirectory => CalculateDirectorySizeAsync(subDirectory));
            size += (await Task.WhenAll(subDirectoryTasks)).Sum();
        }
        catch (UnauthorizedAccessException)
        {
            // Обработка исключения
        }
        return size;
    }

    [HttpGet]
    public async Task<IActionResult> SubDirectoryContents(string directoryPath)
    {
        List<FileSystemItem> subItems = new List<FileSystemItem>();

        try
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
            await ProcessSubDirectoryContentsAsync(directoryInfo, subItems, 1); // Начинаем с первого уровня
        }
        catch (Exception ex)
        {
            // Обработка исключения, например, запись в лог или отправка сообщения об ошибке.
            // subItems останется пустым.
        }

        return PartialView("_SubDirectoryContents", subItems);
    }

    private async Task ProcessSubDirectoryContentsAsync(DirectoryInfo directoryInfo, List<FileSystemItem> items, int level)
    {
        try
        {
            foreach (var file in directoryInfo.GetFiles())
            {
                items.Add(new FileSystemItem
                {
                    Name = file.Name,
                    Path = file.FullName,
                    Size = file.Length,
                    IsDirectory = false,
                    Level = level
                });
            }

            foreach (var subDirectory in directoryInfo.GetDirectories())
            {
                var directorySize = await CalculateDirectorySizeAsync(subDirectory);
                items.Add(new FileSystemItem
                {
                    Name = subDirectory.Name,
                    Path = subDirectory.FullName,
                    Size = directorySize,
                    IsDirectory = true,
                    Level = level
                });

                // Рекурсивно обработать поддиректорию на следующем уровне
                await ProcessSubDirectoryContentsAsync(subDirectory, items, level + 1);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Обработка исключения, например, запись в лог или игнорирование.
        }
    }

    public string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
