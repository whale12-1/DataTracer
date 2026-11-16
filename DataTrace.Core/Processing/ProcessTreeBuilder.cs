using DataTrace.Shared;
using System;
using System.Collections.Generic;

namespace DataTrace.Core.Processing
{
    public class ProcessNode
    {
        public ProcessEvent Process { get; set; } = null!;
        public List<ProcessNode> Children { get; set; } = new();
    }

    public class ProcessTreeBuilder
    {
        public List<ProcessNode> BuildTree(IEnumerable<ProcessEvent> events)
        {
            var nodes = new Dictionary<uint, ProcessNode>();
            var roots = new List<ProcessNode>();

            // 1. Создаем узлы для каждого процесса
            foreach (var evt in events)
            {
                // ВАЖНО: Если PID переиспользовался, мы оставляем только самое новое событие,
                // чтобы не строить дубликаты. В реальном продакшене нужно учитывать время старта.
                nodes[evt.Pid] = new ProcessNode { Process = evt };
            }

            // 2. Связываем узлы (ищем родителя для каждого)
            foreach (var node in nodes.Values)
            {
                uint parentPid = node.Process.ParentPid;

                // Если родитель есть в нашем списке и это не сам процесс
                if (nodes.TryGetValue(parentPid, out var parentNode) && parentPid != node.Process.Pid)
                {
                    parentNode.Children.Add(node);
                }
                else
                {
                    // Если родителя нет (например, родитель был запущен до старта монитора), 
                    // то процесс считается корневым
                    roots.Add(node);
                }
            }

            return roots;
        }

        // Метод для красивого вывода в консоль
        public void PrintTree(List<ProcessNode> nodes, string indent = "")
        {
            foreach (var node in nodes)
            {
                // Для красоты выводим только имя файла, а не весь длинный путь
                var fileName = node.Process.ImageName.Split('\\').Last();
                Console.WriteLine($"{indent}└── {fileName} (PID: {node.Process.Pid})");
                PrintTree(node.Children, indent + "    ");
            }
        }
    }
}