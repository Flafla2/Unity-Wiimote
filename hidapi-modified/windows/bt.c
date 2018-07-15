#include <windows.h>
#include <Bthsdpdef.h>
#include <BluetoothAPIs.h>

#include <math.h>
#include <string.h>

typedef void remote_connect_responder(const char *addr, const char *name);

struct _BTSearcher {
	HANDLE thread_handle;
	HANDLE cancel_event;
	bool running;
	uint64_t start_time; // in ms
	uint64_t duration;   // in seconds
	remote_connect_responder *callback;
};
typedef struct _BTSearcher BTSearcher;

const char *WIIMOTE_NAME = "Nintendo RVL-CNT-01";

BTSearcher *init_bt_search() {
	BTSearcher *ret = malloc(sizeof(BTSearcher));

	if(ret == NULL)
		return NULL;

	ret->thread_handle = NULL;
	ret->running = false;
	ret->callback = NULL;
	ret->cancel_event = CreateEvent( 
        NULL,               // default security attributes
        TRUE,               // manual-reset event
        FALSE,              // initial state is nonsignaled
        NULL                // object name
        );

	if(cancel_event == NULL) {
		free(ret);
		return NULL;
	}

	return ret;
}

void free_bt_search(BTSearcher *search) {
	if(search->running) {
		stop_wiimote_bt_search(search);
	}

	CloseHandle(ret->cancel_event);

	free(search);
}

uint64_t get_time_millis() {
	// https://stackoverflow.com/questions/1695288/getting-the-current-time-in-milliseconds-from-the-system-clock-in-windows
	FILETIME ft;
	GetSystemTimeAsFileTime(&ft);

	// GetSystemTimeAsFileTime returns the number of 100-nanosecond intervals since the Win32
	// epoch.  So we divide by 10000 to convert to milliseconds
	return ((uint64_t)(ft.dwLowDateTime) + ((uint64_t)(ft.dwHighDateTime) << 32)) / 10000;
}

BOOL WINAPI authentication_callback(LPVOID pvParam, PBLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS pAuthCallbackParams) {
	BTSearcher *searcher = (BTSearcher *)pvParam;

	BLUETOOTH_AUTHENTICATE_RESPONSE response;
	BLUETOOTH_DEVICE_INFO device_info = pAuthCallbackParams->deviceInfo;
	response.authMethod = pAuthCallbackParams->authenticationMethod;

	// Make sure we are using passkey authentication (which is what the Wiimote uses)
	if (response.authMethod != BLUETOOTH_AUTHENTICATION_METHOD_PASSKEY)
		return false;
	response.bthAddressRemote = device_info.Address;
    response.negativeResponse = false;

    BLUETOOTH_PIN_INFO pin;
    pin.pin_length = 6;
    for(int x = 0; x < 6; x++) {
    	// Wiimote expects the key to be its MAC address reversed
    	pin.pin[5 - x] = device_info.Address.rgBytes[5 - x];
    }

    DWORD dwRet = BluetoothSendAuthenticationResponseEx(NULL, &response);
    if(dwRet != ERROR_SUCCESS)
    	return false;

    char addr[64];
    char *adb = device_info.Address.rgBytes[5 - x];
    snprintf(addr, 64, "%02x:%02x:%02x:%02x:%02x:%02x", adb[0], adb[1], adb[2], adb[3], adb[4], adb[5]);

    searcher->callback(addr, device_info.szName)

    return true;
}

bool authenticate_wiimote(BLUETOOTH_DEVICE_INFO *device_info, BTSearcher *searcher) {
	// register authentication callback. this prevents UI from showing up.
	HBLUETOOTH_AUTHENTICATION_REGISTRATION hRegHandle;
    DWORD dwRet = BluetoothRegisterForAuthenticationEx(device_info, &hRegHandle, &authentication_callback, searcher);
    if (dwRet != ERROR_SUCCESS)
        return false;

    // authenticate device (will call authentication callback)
    AUTHENTICATION_REQUIREMENTS authreqs = MITMProtectionNotRequired;
    dwRet = BluetoothAuthenticateDeviceEx(NULL, NULL, &btdi, NULL, authreqs);
    if (dwRet != ERROR_SUCCESS)
    	return false;

    return true;
}

void bt_search_thread(void *data) {
	BTSearcher *handle = (BTSearcher *)data;

	BLUETOOTH_DEVICE_INFO device_info;
	device_info.dwSize = sizeof(device_info);

	BLUETOOTH_DEVICE_SEARCH_PARAMS search_criteria;
	search_criteria.dwSize = sizeof(BLUETOOTH_DEVICE_SEARCH_PARAMS);
	search_criteria.fReturnAuthenticated = false;
	search_criteria.fReturnRemembered = false;
	search_criteria.fReturnConnected = false;
	search_criteria.fReturnUnknown = true;
	search_criteria.fIssueInquiry = true;
	search_criteria.hRadio = NULL; // all radios

	// Windows wants the timeout as a multiple of 1.28 seconds (lol wtf)
	// https://docs.microsoft.com/en-us/windows/desktop/api/bluetoothapis/ns-bluetoothapis-_bluetooth_device_search_params
	double multiplier = ceil(((double)data->duration) / 1.28);
	if(multiplier > 48)
		multiplier = 48;
	if(multiplier < 1)
		multiplier = 1;
	search_criteria.cTimeoutMultiplier = (char)(multiplier);

	handle->start_time = get_time_millis();
	uint64_t end_time = handle->start_time + handle->duration * 1000;

	HBLUETOOTH_DEVICE_FIND found = BluetoothFindFirstDevice(&search_criteria, &device_info);

	while(handle->running && get_time_millis() < end_time) {
		while(found != NULL) {
			char *name = device_info.szName;

			if(strlen(name) > strlen(WIIMOTE_NAME) &&
				strncmp(WIIMOTE_NAME, name, strlen(WIIMOTE_NAME)) == 0) {
				// Device name begins with "Nintendo RVL-CNT-01"
				// We have configured our inquiry to not return authenticated devices
				// so we can assume the Wiimote here is unpaired.

				authenticate_wiimote(&device_info);
			}

			found = BluetoothFindNextDevice(found, &device_info);
		}

		// Poll every 500ms
		// TODO: Make this configurable?
		if(WaitForSingleObject(cancel_event, 500) == WAIT_OBJECT_0) {
			// Main thread has signaled a cancel
			handle->running = false;
		}
	}
	
	handle->running = false;
}

void begin_wiimote_bt_search(BTSearcher *searcher, remote_connect_responder *resp, int length) {

	if(searcher->running)
		return;

	searcher->duration = length;
	searcher->running = true;
	searcher->thread_handle = CreateThread(NULL, 0, bt_search_thread, searcher, NULL);
	searcher->callback = resp;

	if(!searcher->thread_handle)
		searcher->running = false;

}

void stop_wiimote_bt_search(BTSearcher *searcher) {

	if(!searcher->running)
		return;

	SetEvent(searcher->cancel_event);
	// Join with searcher thread
	WaitForSingleObject(searcher->thread_handle, 1000);

	if(searcher->running) {
		//Thread failed to join

		//TODO: Handle error
		return;
	}

	searcher->thread_handle = NULL;
}

bool is_searching(BTSearcher *searcher) {

	return searcher->running;

}