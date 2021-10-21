#include <Windows.h>
#include <stdio.h>
#include <tchar.h>
#include <shlwapi.h>

int _tmain(int argc, LPCTSTR argv[])
{
	DWORD bufferSize = 1048576;

	// command line syntax: secreplace.exe filetowriteto.ext startpos filetoreadfrom.ext
	if (argc != 4)
	{
		_tprintf(_T("Command line argument number incorrect.\n"));
		_tprintf(_T("Syntax: secreplace.exe FileNameToWriteTo StartPosition FileNameToReadFrom\n"));
		return 1;
	}

	// convert write file pointer large integer
	LONGLONG fileToWriteStartPos = 0;
	if (!StrToInt64Ex(argv[2], STIF_DEFAULT, &fileToWriteStartPos))
	{
		_tprintf(_T("Error converting file to write start position to integer.\n"));
		return 1;
	}
	_tprintf(_T("fileToWriteStartPos: %lld\n"), fileToWriteStartPos);

	BOOL bResult = FALSE;
	// allocate memory
	LPVOID memory = VirtualAlloc(NULL, bufferSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
	if (NULL == memory)
	{
		_tprintf(_T("Error allocating memory.\n"));
	}
	else
	{
		bResult = TRUE;
	}

	HANDLE hFileRead = INVALID_HANDLE_VALUE;
	if (bResult)
	{
		// open file to read
		_tprintf(_T("Opening file to read from: %s\n"), argv[3]);
		hFileRead = CreateFile(argv[3], GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (INVALID_HANDLE_VALUE == hFileRead)
		{
			_tprintf(_T("Error opening file to read from.\n"));
			bResult = FALSE;
		}
	}

	HANDLE hFileWrite = INVALID_HANDLE_VALUE;
	if (bResult)
	{
		// open file to write
		_tprintf(_T("Opening file to write to: %s\n"), argv[1]);
		hFileWrite = CreateFile(argv[1], GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (INVALID_HANDLE_VALUE == hFileWrite)
		{
			_tprintf(_T("Error opening file to write to.\n"));
			bResult = FALSE;
		}
	}

	LARGE_INTEGER liFileToWriteStartPos;
	liFileToWriteStartPos.QuadPart = fileToWriteStartPos;
	if (bResult)
	{
		// set file pointer
		if (!SetFilePointerEx(hFileWrite, liFileToWriteStartPos, NULL, FILE_BEGIN))
		{
			_tprintf(_T("Error setting write file pointer.\n"));
			bResult = FALSE;
		}
	}

	// copy the section
	DWORD nNumberOfBytesRead = 0;
	DWORD nNumberOfBytesWritten = 0;
	if (bResult)
	{
		while (TRUE)
		{
			// read to buffer
			if (!ReadFile(hFileRead, memory, bufferSize, &nNumberOfBytesRead, NULL))
			{
				_tprintf(_T("Error reading file.\n"));
				bResult = FALSE;
			}
			if (bResult)
			{
				_tprintf(_T("Bytes read from file: %d\n"), nNumberOfBytesRead);
				// write buffer to file
				if (nNumberOfBytesRead > 0)
				{
					if (!WriteFile(hFileWrite, memory, nNumberOfBytesRead, &nNumberOfBytesWritten, NULL))
					{
						_tprintf(_T("Error writing file.\n"));
						bResult = FALSE;
					}
					if (bResult)
					{
						_tprintf(_T("Bytes written to file: %d\n"), nNumberOfBytesWritten);
						if (nNumberOfBytesWritten != nNumberOfBytesRead)
						{
							_tprintf(_T("Partial write occurred.\n"));
						}
					}
				}
			}
			if (bResult)
			{
				if (nNumberOfBytesRead < bufferSize) break;
			}
			else break;
		}
	}

	if (hFileWrite != INVALID_HANDLE_VALUE) CloseHandle(hFileWrite);
	if (hFileRead != INVALID_HANDLE_VALUE) CloseHandle(hFileRead);
	// free memory
	if (memory) VirtualFree(memory, 0, MEM_RELEASE);
	return 0;
}
