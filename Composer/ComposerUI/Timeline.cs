// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Singletons;

namespace XanaduProject.Composer.ComposerUI
{
    [SuperNode(typeof(Dependent))]
    public partial class Timeline : ScrollContainer
    {
        public override partial void _Notification(int what);

        private const float height = 150;
        private const float separation_ratio = 500;

        private Container container = new Container();

        [Dependency] private AudioSource audioSource => DependOn<AudioSource>();

        public override void _Process(double delta)
        {
            base._Process(delta);

            QueueRedraw();
            ScrollHorizontal = (int)(container.CustomMinimumSize.X * (audioSource.GetPlaybackPosition() / audioSource.Stream.GetLength()));
        }

        public void OnResolved()
        {
            AddChild(container);
            container.CustomMinimumSize = new Vector2((float)audioSource.Stream.GetLength() * separation_ratio, 150);
        }

        public override void _Draw()
        {
            float lineRatio = (float)(60f / audioSource.Bpm * separation_ratio);

            base._Draw();

            float lastLinePosition = 0;

            while (lastLinePosition < container.CustomMinimumSize.X)
            {
                DrawLine(new Vector2(container.Position.X + lastLinePosition + lineRatio, 0), new Vector2(container.Position.X + lastLinePosition + lineRatio, height), Colors.LightGray, 1, true);
                lastLinePosition += lineRatio;
            }
        }
    }
}
