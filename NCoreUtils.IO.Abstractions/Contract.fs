namespace NCoreUtils.IO

open System
open System.IO

/// Defines functionality for implementing asynchronous stream producer.
type IStreamProducer =
  inherit IDisposable
  /// <summary>
  /// Populates contents of the output stream.
  /// </summary>
  /// <param name="output">Output stream.</param>
  abstract AsyncProduce : output:Stream -> Async<unit>

/// Defines functionality for implementing asynchronous stream transformation.
type IStreamTransformation =
  inherit IDisposable
  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="input">Input stream.</param>
  /// <param name="output">Output stream.</param>
  abstract AsyncPerform : input:Stream * output:Stream -> Async<unit>

/// Defines functionality for implementing asynchronous stream consumer.
type IStreamConsumer =
  inherit IDisposable
  /// <summary>
  /// Consumes the input stream.
  /// </summary>
  /// <param name="input">Input stream.</param>
  abstract AsyncConsume : input:Stream -> Async<unit>

/// Defines functionality for implementing dependent asynchronous stream transformation.
type IDependentStreamTransformation<'TState> =
  inherit IDisposable
  /// <summary>
  /// Performs the transformation defined by the actual instance.
  /// </summary>
  /// <param name="input">Input stream.</param>
  /// <param name="dependentOutput">Function to populate output stream depending on the intermediate state.</param>
  abstract AsyncPerform : input:Stream * dependentOutput:('TState -> Stream) -> Async<unit>
