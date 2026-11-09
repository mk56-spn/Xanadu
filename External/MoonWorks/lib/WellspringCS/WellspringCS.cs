/* WellspringCS - C# bindings for the Wellspring font rendering Library
 *
 * Copyright (c) 2022 Evan Hemsley
 *
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from
 * the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 * claim that you wrote the original software. If you use this software in a
 * product, an acknowledgment in the product documentation would be
 * appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not be
 * misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source distribution.
 *
 * Evan "cosmonaut" Hemsley <evan@moonside.games>
 *
 */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WellspringCS
{
	public static partial class Wellspring
	{
		private const string nativeLibName = "Wellspring";

		// Version

		public const uint WELLSPRING_MAJOR_VERSION = 1;
		public const uint WELLSPRING_MINOR_VERSION = 0;
		public const uint WELLSPRING_PATCH_VERSION = 1;

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial uint Wellspring_LinkedVersion();

		[StructLayout(LayoutKind.Sequential)]
		public struct FontRange
		{
			public uint FirstCodepoint;
			public uint NumChars;
			public byte OversampleH;
			public byte OversampleV;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Vertex
		{
			public float X;
			public float Y;
			public float U;
			public float V;
			public uint ChunkSize;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Rectangle
		{
			public float X;
			public float Y;
			public float W;
			public float H;
		}

		public enum HorizontalAlignment
		{
			Left,
			Center,
			Right
		}

		public enum VerticalAlignment
		{
			Baseline,
			Top,
			Middle,
			Bottom
		}

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial IntPtr Wellspring_CreateFont(
			IntPtr fontBytes,
			uint fontBytesLength,
			IntPtr atlasJsonBytes,
			uint atlasJsonBytesLength,
			out float pixelsPerEm,
			out float distanceRange
		);

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial IntPtr Wellspring_CreateTextBatch();

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial void Wellspring_StartTextBatch(
			IntPtr textBatch
		);

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial byte Wellspring_TextBounds(
			IntPtr font,
			int pixelSize,
			HorizontalAlignment horizontalAlignment,
			VerticalAlignment verticalAlignment,
			IntPtr strBytes, /* UTF-8 bytes */
			uint strLengthInBytes,
			out Rectangle rectangle
		);

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial byte Wellspring_AddChunkToTextBatch(
			IntPtr textBatch,
			IntPtr font,
			int pixelSize,
			HorizontalAlignment horizontalAlignment,
			VerticalAlignment verticalAlignment,
			IntPtr strBytes, /* UTF-8 bytes */
			uint strLengthInBytes
		);

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial void Wellspring_GetBufferData(
			IntPtr textBatch,
			out uint vertexCount,
			out IntPtr vertexDataPointer
		);

		public static unsafe void Wellspring_GetBufferData(
			IntPtr textBatch,
			out Span<Vertex> vertices
		) {
			Wellspring_GetBufferData(
				textBatch,
				out var vertexCount,
				out var vertexDataPointer
			);

			vertices = new Span<Vertex>((void*) vertexDataPointer, (int) vertexCount);
		}

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial void Wellspring_DestroyTextBatch(
			IntPtr textBatch
		);

		[LibraryImport(nativeLibName)]
		[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial void Wellspring_DestroyFont(
			IntPtr font
		);
	}
}
