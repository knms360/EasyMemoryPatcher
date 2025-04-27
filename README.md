# EasyMemoryPatcher
## This program is a console application port of Memory.dll.This program is a console application port of [Memory.dll](https://github.com/erfg12/memory.dll). Thank you for erfg12.

This is a console application that allows you to control memory from common executable programs such as the command prompt or bat files.

Many arguments are based on [https://github.com/erfg12/memory.dll/wiki/WriteMemory].

Note: You may need to run as administrator and match the architecture to the target process, but this is not required.

Also, x86 applications cannot access x64 processes.

# How to Use
Watch WiKi.

You can also see simple usage by using /h.

     MemoryPatcher WriteMemory 0x21C14E3E byte 0xFF /pid 14052
     MemoryPatcher.exe ReadBytes 0x21C14E3E 5 /pid 14052
     C:\MemoryPatcher.exe ReadBits 0x21C14E3E /pname pcsx2.exe
     MemoryPatcher WriteBits 0x21C14E3E 01111110 /pname pcsx2
     MemoryPatcher AoBScan ""F9 11 39 44 B3 ?? 8F 3F C3 11"" /pname pcsx2.exe
     MemoryPatcher CheckProcess /pname pcsx2.exe
