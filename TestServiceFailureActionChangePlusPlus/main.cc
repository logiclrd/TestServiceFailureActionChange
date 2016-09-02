#include <Windows.h>
#include <Shlwapi.h>
#include <ShlObj.h>

#include <iostream>
#include <iomanip>
#include <sstream>
#include <string>
#include <vector>

using namespace std;

wstring *GetErrorMessageWithErrorCode(const wstring &message, HRESULT result = S_OK)
{
	if (result != S_OK)
	{
		wstringstream errorMessageBuilder;

		errorMessageBuilder << message << L" failed with HRESULT " << hex << setw(8) << setfill(L'0') << result;

		throw new wstring(errorMessageBuilder.str());
	}
	else
	{
		DWORD errorCode = GetLastError();

		wstringstream errorMessageBuilder;

		errorMessageBuilder << message << L" failed with error code " << errorCode;

		throw new wstring(errorMessageBuilder.str());
	}
}

wstring GetFileName(const wstring &relativePath)
{
	DWORD charsRequired = GetFullPathNameW(relativePath.c_str(), 0, NULL, NULL);

	if (charsRequired == 0)
		throw GetErrorMessageWithErrorCode(L"When determining the buffer size for the full path name for the application program file, GetFullPathNameW");

	vector<wchar_t> buffer;

	buffer.resize(charsRequired);

	wchar_t *fileNamePtr;

	DWORD charsCopied = GetFullPathNameW(relativePath.c_str(), buffer.size(), &buffer[0], &fileNamePtr);

	if (charsCopied == 0)
		throw GetErrorMessageWithErrorCode(L"When retrieving full path name for the application program file, GetFullPathNameW");

	return fileNamePtr;
}

wstring GetPathToPingExecutable()
{
	wchar_t *systemPath;

	HRESULT result = SHGetKnownFolderPath(
		FOLDERID_System,
		KF_FLAG_DONT_UNEXPAND | KF_FLAG_DONT_VERIFY,
		NULL, // hToken: current user
		&systemPath);

	if (FAILED(result))
		throw GetErrorMessageWithErrorCode(L"When retrieving System32 path, SHGetKnownFolderPath", result);

	struct Deallocator
	{
		wchar_t *ptr;

		~Deallocator() { CoTaskMemFree(ptr); }
	} systemPathDeallocator = { systemPath };

	wstring combinedPath;

	combinedPath.resize(wcslen(systemPath) + 10, L'\0');

	PathCombineW(&combinedPath[0], systemPath, L"ping.exe");

	combinedPath.resize(wcslen(&combinedPath[0]));

	return combinedPath;
}

wstring EscapeArg(const wstring &arg)
{
	if (arg.find_first_of(L" \t\"") == wstring::npos)
		return arg;
	else
	{
		wstring escapedArg;

		escapedArg.reserve(arg.size() + 2);
		escapedArg.append(1, L' ').append(arg).append(1, L' ');

		wstring::size_type quoteCharIndex = escapedArg.find(L'"');

		while (quoteCharIndex != wstring::npos)
		{
			escapedArg.insert(quoteCharIndex, 1, L'\\');
			quoteCharIndex = escapedArg.find(L'"', quoteCharIndex + 2);
		}

		escapedArg[0] = escapedArg[escapedArg.size() - 1] = '"';

		return escapedArg;
	}
}

int wmain(int argc, wchar_t *argv[])
{
	try
	{
		if (argc != 2)
		{
			wcout << L"Usage: " << GetFileName(argv[0]) << L" <name of service to adjust>" << endl;
			wcout << L"If you'd like to run this test against a dummy service, use these commands:" << endl;
			wcout << L"=> Create:   sc create TestSvc binPath= " << EscapeArg(GetPathToPingExecutable()) << L" start= demand" << endl;
			wcout << L"=> Remove:   sc delete TestSvc" << endl;

			return 2;
		}

		wcout << L"Adjusting service failure actions for service: " << argv[1] << endl;

		SC_HANDLE hSCManager = OpenSCManagerW(NULL, SERVICES_ACTIVE_DATABASEW, SC_MANAGER_ALL_ACCESS);

		if (hSCManager == NULL)
			throw GetErrorMessageWithErrorCode(L"When connecting to the Service Control Manager, OpenSCManagerW");

		SC_HANDLE hService = OpenServiceW(hSCManager, argv[1], SERVICE_ALL_ACCESS);

		if (hService == NULL)
			throw GetErrorMessageWithErrorCode(wstring(L"When opening service ") + argv[1] + L", OpenServiceW");

		SC_ACTION actionSteps[6];

		actionSteps[0].Delay = 5000;
		actionSteps[0].Type = SC_ACTION_RESTART;

		actionSteps[1].Delay = 15000;
		actionSteps[1].Type = SC_ACTION_RESTART;

		actionSteps[2].Delay = 25000;
		actionSteps[2].Type = SC_ACTION_RESTART;

		actionSteps[3].Delay = 35000;
		actionSteps[3].Type = SC_ACTION_RESTART;

		actionSteps[4].Delay = 45000;
		actionSteps[4].Type = SC_ACTION_RESTART;

		actionSteps[5].Delay = 0;
		actionSteps[5].Type = SC_ACTION_NONE;

		SERVICE_FAILURE_ACTIONSW actions;

		actions.dwResetPeriod = 60000;
		actions.lpCommand = NULL;
		actions.lpRebootMsg = NULL;
		actions.cActions = 6;
		actions.lpsaActions = &actionSteps[0];

		BOOL result = ChangeServiceConfig2W(hService, SERVICE_CONFIG_FAILURE_ACTIONS, &actions);

		if (!result)
			throw GetErrorMessageWithErrorCode(L"When setting SERVICE_CONFIG_FAILURE_ACTIONS, ChangeServiceConfig2W");

		SERVICE_FAILURE_ACTIONS_FLAG flag;

		flag.fFailureActionsOnNonCrashFailures = false;

		result = ChangeServiceConfig2W(hService, SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, &flag);

		if (!result)
			throw GetErrorMessageWithErrorCode(L"When setting SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, ChangeServiceConfig2W");

		wcout << L"The operation appears to have succeeded." << endl;
	}
	catch (wstring *errorMessage)
	{
		wcout << L"ERROR:" << endl;
		wcout << *errorMessage << endl;

		delete errorMessage;

		return 1;
	}
}
