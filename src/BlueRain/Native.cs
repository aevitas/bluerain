// Copyright (C) 2013-2015 aevitas
// See the file COPYING for copying permission.

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
	public enum ProcessAccessFlags : uint
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
	public enum ProtectionFlags : uint
	{
		Execute = 0x10,
		PageExecuteRead = 0x20,
		PageExecuteReadWrite = 0x40,
		PageExecuteWriteCopy = 0x80,
		PageNoAccess = 0x01,
		PageReadOnly = 0x02,
		PageReadWrite = 0x04,
		PageWriteCopy = 0x08
	}
}