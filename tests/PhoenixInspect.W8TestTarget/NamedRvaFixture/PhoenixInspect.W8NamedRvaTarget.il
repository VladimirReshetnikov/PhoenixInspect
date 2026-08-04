.assembly extern System.Runtime
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A)
  .ver 10:0:0:0
}

.assembly PhoenixInspect.W8NamedRvaTarget
{
  .ver 1:0:0:0
}

.module PhoenixInspect.W8NamedRvaTarget.dll

.class public abstract sealed auto ansi beforefieldinit PhoenixInspect.W8NamedRvaTarget.NamedRvaStorage
       extends [System.Runtime]System.Object
{
  .field public static initonly int32 NamedSentinel at D_0000
  .field public static initonly int64 NamedWideSentinel at D_0008
}

.data D_0000 = bytearray (61 7A 04 21)
.data D_0008 = bytearray (92 7A 08 42 19 A7 E5 D3)
