// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;

// ReSharper disable InconsistentNaming

namespace BlueRain
{
	[Flags]
	public enum ThreadAccess : uint
	{
		TERMINATE = (0x0001),
		SUSPEND_RESUME = (0x0002),
		GET_CONTEXT = (0x0008),
		SET_CONTEXT = (0x0010),
		SET_INFORMATION = (0x0020),
		QUERY_INFORMATION = (0x0040),
		SET_THREAD_TOKEN = (0x0080),
		IMPERSONATE = (0x0100),
		DIRECT_IMPERSONATION = (0x0200),
		ALL = 0xFFF
	}

	[Flags]
	public enum ProcessAccess : uint
	{
		All = 0x001F0FFF,
		Terminate = 0x00000001,
		CreateThread = 0x00000002,
		VMOperation = 0x00000008,
		VMRead = 0x00000010,
		VMWrite = 0x00000020,
		DupHandle = 0x00000040,
		SetInformation = 0x00000200,
		QueryInformation = 0x00000400,
		Synchronize = 0x00100000
	}

	[Flags]
	public enum AllocationType
	{
		Commit = 0x1000,
		Reserve = 0x2000,
		Decommit = 0x4000,
		Release = 0x8000,
		Reset = 0x80000,
		Physical = 0x400000,
		TopDown = 0x100000,
		WriteWatch = 0x200000,
		LargePages = 0x20000000
	}

	[Flags]
	public enum MemoryProtection
	{
		Execute = 0x10,
		ExecuteRead = 0x20,
		ExecuteReadWrite = 0x40,
		ExecuteWriteCopy = 0x80,
		NoAccess = 0x01,
		ReadOnly = 0x02,
		ReadWrite = 0x04,
		WriteCopy = 0x08,
		GuardModifierflag = 0x100,
		NoCacheModifierflag = 0x200,
		WriteCombineModifierflag = 0x400
	}

	[Flags]
	public enum FreeType
	{
		Decommit = 0x4000,
		Release = 0x8000
	}

	[Flags]
	public enum LoadLibraryExOptions : uint
	{
		DontResolveDllReferences = 0x00000001,
		LoadLibraryAsDatafile = 0x00000002,
		LoadLibraryWithAlteredSearchPath = 0x00000008,
		LoadIgnoreCodeAuthzLevel = 0x00000010,
		LoadLibraryAsImageResource = 0x00000020,
		LoadLibraryAsDatafileExclusive = 0x00000040
	}
}