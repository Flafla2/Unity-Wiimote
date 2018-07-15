#import <IOBluetooth/IOBluetooth.h>
#import <IOBluetooth/objc/IOBluetoothDeviceInquiry.h>
#import <IOBluetooth/objc/IOBluetoothDevice.h>

#import <IOKit/hid/IOHIDLib.h>
#import <IOKit/hid/IOHIDUsageTables.h>

#import <objc/runtime.h>

#import <AppKit/NSAlert.h>

typedef void remote_connect_responder(const char *addr, const char *name);

@interface BTSearcher : NSObject <IOBluetoothDeviceInquiryDelegate>

- (void)startWiimoteSearch:(remote_connect_responder*)callback;
- (void)stopWiimoteSearch;
- (void)closeAllConnections;
- (BTSearcher *)init;
- (bool)isSearching;
- (NSString *)flushLog;

@end

@interface IOBluetoothDevicePair (SyncStyle)
@property(nonatomic, assign) BOOL attemptedHostToDevice;
@end

static const void * kOEBluetoothDevicePairSyncStyleKey = &kOEBluetoothDevicePairSyncStyleKey;

@implementation IOBluetoothDevicePair (SyncStyle)

- (BOOL)attemptedHostToDevice
{
    return [objc_getAssociatedObject(self, kOEBluetoothDevicePairSyncStyleKey) boolValue];
}

- (void)setAttemptedHostToDevice:(BOOL)attemptedHostToDevice
{
    objc_setAssociatedObject(self, kOEBluetoothDevicePairSyncStyleKey, @(attemptedHostToDevice), OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

@end

@implementation BTSearcher {
    // Currently Active Bluetooth device inquiry (or nil)
    IOBluetoothDeviceInquiry *_inquiry;
    // Queue of NSStrings to log in Unity
    NSMutableArray *_log;
    // Unity callback
    remote_connect_responder *_callback;
    // True if we are currently doing a device inquiry
    bool _searching;
    // Array of all currently connected IOBluetoothDevices
    NSMutableArray *_connectedDevices;
}

- (BTSearcher *)init 
{
    if (self = [super init]) {
        // Initialize self
        _searching = false;
        _log = [[NSMutableArray alloc] init];
    }

    return self;
}

- (void)log:(NSString *)str
{
    [_log addObject:str];
}

- (NSString *)flushLog
{   
    NSMutableString *str = [[NSMutableString alloc] init];
    for(int x = 0; x < [_log count]; x++) {
        [str appendString:[_log objectAtIndex:x]];
        [str appendString:@"\n"];
    }

    [_log removeAllObjects];

    [str autorelease];
    return str;
}

- (void)startWiimoteSearch:(remote_connect_responder*)callback withLength:(int)length
{
    @synchronized(self) {
        if(_searching) {
            [self log:[NSString stringWithFormat:@"Tried to start wiimote search while search was already happening!"]];
            return;
        }

        //[self log:[NSString stringWithFormat:@"Searching for Wiimotes"]];
        _searching = true;
        _callback = callback;

        _inquiry = [IOBluetoothDeviceInquiry inquiryWithDelegate:self];
        [_inquiry setInquiryLength:length];
        [_inquiry setUpdateNewDeviceNames:YES];

        IOReturn status = [_inquiry start];
        if(status == kIOReturnSuccess)
            return;

        [_inquiry setDelegate:nil];
        _inquiry = nil;
        [self log:[NSString stringWithFormat:@"Error: Inquiry did not start, error %d", status]];
    }
}

- (void)stopWiimoteSearch;
{
    @synchronized(self) {
        if(!_searching) {
            [self log:[NSString stringWithFormat:@"Tried to stop wiimote search while no search was already happening!"]];
            return;
        }

        if(_inquiry != nil && _searching) {
            [_inquiry stop];
            [_inquiry setDelegate:nil];
        }
        
        _inquiry = nil;
        _callback = nil;
        _searching = false;
    }
}

- (void)closeAllConnections;
{
    [_connectedDevices enumerateObjectsUsingBlock:^(IOBluetoothDevice *obj, NSUInteger idx, BOOL * _Nonnull stop) {
        if([obj isConnected])
            [obj closeConnection];
    }];

    [_connectedDevices removeAllObjects];
}

- (bool)isSearching;
{
    return _searching;
}

- (void)blockingAlert:(NSString *)message
{
    NSAlert *alert = [[NSAlert alloc] init];
    [alert addButtonWithTitle:@"OK"];
    [alert setMessageText:@"Blocking debug message"];
    [alert setInformativeText:message];

    [alert runModal];

    [alert release];
}

- (void)deviceInquiryDeviceFound:(IOBluetoothDeviceInquiry *)sender device:(IOBluetoothDevice *)device
{
    [self log:[NSString stringWithFormat:@"FOUND DEVICE: %@ %@", NSStringFromSelector(_cmd), device]];
    // We do not stop the inquiry here because we want to find multiple Wii Remotes, and also because
    // our search criteria is wide, and we may find non-Wiimotes.

    if(![[device name] hasPrefix:@"Nintendo RVL-CNT-01"])
            return;

    if([device isConnected]) {
        [self log:@"Found Wiimote in inquiry, but it was already connected."];
        return;
    }

    //[device openConnection];

    [self log:@"Found a wiimote!"];
    if([device isPaired]) {
        [self log:@"...but it's already paired."];
        return;
    }
    [self log:@"...initiating pair"];
    
    IOBluetoothDevicePair *pair = [IOBluetoothDevicePair pairWithDevice:device];

    [pair setDelegate:self];
    [pair start];
}

- (void)deviceInquiryComplete:(IOBluetoothDeviceInquiry *)sender error:(IOReturn)error aborted:(BOOL)aborted
{
    //[self log:[NSString stringWithFormat:@"Devices: %@ Error: %d, Aborted: %s", [sender foundDevices], error, BOOL_STR(aborted)]];

    // [[sender foundDevices] enumerateObjectsUsingBlock:^(IOBluetoothDevice *obj, NSUInteger idx, BOOL *stop) {

    //     // Check to make sure BT device name has Wiimote prefix. Note that there are multiple
    //     // possible device names ("Nintendo RVL-CNT-01" and "Nintendo RVL-CNT-01-TR" at the
    //     // time of writing), so we don't do an exact string match.
    //     if(![[obj name] hasPrefix:@"Nintendo RVL-CNT-01"])
    //         return;

    //     [obj openConnection];
    //     [self log:@"Found a wiimote!"];
    //     if([obj isPaired]) {
    //         [self log:@"...but it's already paired."];
    //         return;
    //     }
    //     [self log:@"...initiating pair"];
        
    //     IOBluetoothDevicePair *pair = [IOBluetoothDevicePair pairWithDevice:obj];
    //     [pair setAttemptedHostToDevice:YES];
    //     [pair setDelegate:self];
    //     [pair start];
        
    // }];

    _inquiry = nil;
    _searching = false;
}

- (void)devicePairingPINCodeRequest:(IOBluetoothDevicePair*)sender
{
    [self log:[NSString stringWithFormat:@"Received PIN code request from Wiimote."]];
    
    NSString *localAddress = [[[IOBluetoothHostController defaultController] addressAsString] uppercaseString];
    NSString *remoteAddress = [[[sender device] addressString] uppercaseString];

    BluetoothPINCode code;
    NSScanner *scanner = [NSScanner scannerWithString:[sender attemptedHostToDevice]?localAddress:remoteAddress];
    int byte = 5;
    while(![scanner isAtEnd]) {
        unsigned int data;
        [scanner scanHexInt:&data];
        code.data[byte] = data;
        [scanner scanUpToCharactersFromSet:[NSCharacterSet characterSetWithCharactersInString:@"0123456789ABCDEF"] intoString:nil];
        byte--;
    }

    [sender replyPINCode:6 PINCode:&code];
}

- (void)devicePairingFinished:(IOBluetoothDevicePair*)sender error:(IOReturn)error
{

    if(error == kIOReturnSuccess) {
        NSString *remoteAddress = [[sender device] addressString];
        NSString *remoteName = [[sender device] name];

        [self log:[NSString stringWithFormat:@"Pairing finished %@: %x", sender, error]];
        [_connectedDevices addObject:[[sender device] retain]];

        _callback([remoteAddress UTF8String], [remoteName UTF8String]);
        _callback = nil;
    } else if(![sender attemptedHostToDevice]) {
        [self log:[NSString stringWithFormat:@"Pairing failed, attempting inverse"]];
        IOBluetoothDevicePair *pair = [IOBluetoothDevicePair pairWithDevice:[sender device]];

        [pair setAttemptedHostToDevice:YES];
        [pair setDelegate:self];
        [pair start];
    } else {
        [self log:[NSString stringWithFormat:@"Couldn't pair, what gives?"]];
        _callback = nil;
    }
}

@end


BTSearcher *init_bt_search() {

    return [[BTSearcher alloc] init];

}

const char *get_debug_log(BTSearcher *search) {

    NSString *log = [search flushLog];
    if(log != nil) {
        const char* ret_cpy = [log UTF8String];
        char *ret = malloc([log length] + 1);
        strcpy(ret, ret_cpy);
        
        return ret;
    } else
        return "";
}

void free_bt_search(BTSearcher *search) {

    [search closeAllConnections];
    [search release];

}

void begin_wiimote_bt_search(BTSearcher *searcher, remote_connect_responder *resp, int length){

    [searcher startWiimoteSearch:resp withLength:length];

}

void stop_wiimote_bt_search(BTSearcher *searcher) {

    [searcher stopWiimoteSearch];

}

bool is_searching(BTSearcher *searcher) {

    return [searcher isSearching];

}