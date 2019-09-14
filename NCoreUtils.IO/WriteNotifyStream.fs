namespace NCoreUtils.IO

open System
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading

type internal WriteNotifyStream (baseStream : Stream, [<Optional>] leaveOpen : bool) =
  inherit Stream ()
  let started = Event<EventHandler<_>, EventArgs> ()
  let mutable debounce = 0
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member private this.Trigger () =
    if 0 = Interlocked.CompareExchange (&debounce, 1, 0) then
      started.Trigger (this, EventArgs.Empty)
  [<CLIEvent>]
  member __.Started = started.Publish
  member val BaseStream = baseStream
  override this.CanRead = this.BaseStream.CanRead
  override this.CanSeek = this.BaseStream.CanSeek
  override this.CanTimeout = this.BaseStream.CanTimeout
  override this.CanWrite = this.BaseStream.CanWrite
  override this.Length = this.BaseStream.Length
  override this.Position with get () = this.BaseStream.Position and set value = this.BaseStream.Position <- value
  override this.ReadTimeout with get () = this.BaseStream.ReadTimeout and set value = this.BaseStream.ReadTimeout <- value
  override this.WriteTimeout with get () = this.BaseStream.WriteTimeout and set value = this.BaseStream.WriteTimeout <- value
  override this.BeginRead (buffer, offset, count, callback, state) = this.BaseStream.BeginRead (buffer, offset, count, callback, state)
  override this.BeginWrite (buffer, offset, count, callback, state) =
    this.Trigger ()
    this.BaseStream.BeginWrite (buffer, offset, count, callback, state)
  override this.Close () = this.BaseStream.Close ()
  override this.CopyToAsync (destination, bufferSize, cancellationToken) = this.BaseStream.CopyToAsync (destination, bufferSize, cancellationToken)
  override this.Dispose disposing =
    if disposing && not leaveOpen then
      this.BaseStream.Dispose ()
  override this.EndRead asyncResult = this.BaseStream.EndRead asyncResult
  override this.EndWrite asyncResult = this.BaseStream.EndWrite asyncResult
  override this.Flush () =
    this.Trigger ()
    this.BaseStream.Flush ()
  override this.FlushAsync cancellationToken =
    this.Trigger ()
    this.BaseStream.FlushAsync cancellationToken
  override this.Read (buffer, offset, count) = this.BaseStream.Read (buffer, offset, count)
  override this.ReadAsync (buffer, offset, count, cancellationToken) = this.BaseStream.ReadAsync (buffer, offset, count, cancellationToken)
  override this.ReadByte () = this.BaseStream.ReadByte ()
  override this.Seek (offset, origin) = this.BaseStream.Seek (offset, origin)
  override this.SetLength value = this.BaseStream.SetLength value
  override this.Write (buffer, offset, count) =
    this.Trigger ()
    this.BaseStream.Write (buffer, offset, count)
  override this.WriteAsync (buffer, offset, count, cancellationToken) =
    this.Trigger ()
    this.BaseStream.WriteAsync (buffer, offset, count, cancellationToken)
  override this.WriteByte value =
    this.Trigger ()
    this.BaseStream.WriteByte value
