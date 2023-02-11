﻿using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Memory.Types;

public class Instruction : MemoryObject
{
    private readonly byte[] _originalBytes, _realOriginalBytes, _newBytes, _nopBytes, _retBytes;
    private readonly bool _toggleWithRet;
    private readonly string _signature;
    private nuint _signatureAddress;

    public bool IsPatched =>
        !M.ReadArrayMemory<byte>(AddressPtr, _originalBytes.Length)
            .SequenceEqual(_originalBytes);

    public Instruction(string address, string offsets, byte[] originalBytes, byte[] newBytes = null,
        bool toggleWithRet = false, string signature = "", Mem m = null)
        : base(address, offsets, m)
    {
        _originalBytes = originalBytes;
        _newBytes = newBytes;
        _toggleWithRet = toggleWithRet;
        _signature = signature;


        _retBytes = new byte[_originalBytes.Length];
        _retBytes[0] = 0xC3;
        for (int i = 1; i < _retBytes.Length; i++)
            _retBytes[i] = 0x90;

        _nopBytes = new byte[_originalBytes.Length];
        for (int i = 0; i < _nopBytes.Length; i++)
            _nopBytes[i] = 0x90;

        _realOriginalBytes = M.ReadArrayMemory<byte>(AddressPtr, _originalBytes.Length);
        if (signature != "")
            _signatureAddress = M.AoBScan(_signature).Result.FirstOrDefault();
    }

    public void Nop() => M.WriteArrayMemory(AddressPtr, _nopBytes);

    public void Restore() => M.WriteArrayMemory(AddressPtr, _originalBytes);

    public void Patch() => M.WriteArrayMemory(AddressPtr, _newBytes);

    public void Return() => M.WriteArrayMemory(AddressPtr, _retBytes);

    public void Toggle()
    {
        byte[] currentBytes = M.ReadArrayMemory<byte>(AddressPtr, _originalBytes.Length);

        switch (true)
        {
            case true when currentBytes.SequenceEqual(_nopBytes) || currentBytes.SequenceEqual(_newBytes) ||
                           currentBytes.SequenceEqual(_retBytes):
                Restore();
                break;
            case true when _newBytes != null:
                Patch();
                break;
            case true when _toggleWithRet:
                Return();
                break;
            default:
                Nop();
                break;
        }
    }

    public bool AreBytesAtAddressCorrect() => _originalBytes.SequenceEqual(_realOriginalBytes);

    public void UpdateSignatureAddress(bool myScanner)
    {
        if (myScanner)
            _signatureAddress = MyScan(_signature).FirstOrDefault();

        _signatureAddress = M.AoBScan(_signature).Result.FirstOrDefault();

        AddressPtr = _signatureAddress;
    }

    private IEnumerable<nuint> MyScan(string signature)
    {
        byte[] sig = signature.Split(' ').Select(x => x == "?" ? (byte)0 : byte.Parse(x, NumberStyles.HexNumber)).ToArray();
        bool[] mask = signature.Split(' ').Select(x => x == "?").ToArray();
        byte[] bytes = new byte[M.MProc.Process.MainModule!.ModuleMemorySize];
        Imps.ReadProcessMemory(M.MProc.Handle, (nuint)M.MProc.Process.MainModule.BaseAddress, bytes, bytes.Length);

        for (int i = 0; i < bytes.Length; i++)
        {
            bool found = true;
            
            for (int j = 0; j < sig.Length; j++)
            {
                if (!mask[j] || bytes[i + j] == sig[j]) continue;
                found = false;
                break;
            }

            if (found)
                yield return (nuint)i;
        }
    }
}