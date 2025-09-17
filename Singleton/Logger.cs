using Godot;
using Xanadu.Singletons;
using XanaduProject.Tools;

namespace XanaduProject.Singleton
{
    public static class Logger
    {
        private static readonly LoggerOverlay logger_overlay = new();

        static Logger()
        {
            CanvasLayer layer = new(){ Layer = 100};
            layer.AddChild(logger_overlay);
            GodotTree.Tree.Root.CallDeferred(Node.MethodName.AddChild, layer);
        }

        public static void Boot(){}
        public static void AddLog(LogCategory category, string message)
        {
            var logMessages = logger_overlay.LogMessages[category];
            logMessages.Enqueue(message);
            while (logMessages.Count > 200)
            {
                logMessages.Dequeue();
            }

            logger_overlay.SetLogDirty();
        }
    }
}
