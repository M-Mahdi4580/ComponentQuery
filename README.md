# ComponentQuery
Advanced garbage-free component iteration and retrieval for Unity game objects.

Features:
- Easy to use with 'foreach' loops
- No garbage creation
- Powerful query parameters
- Support for covariance and contravariance of interface types

Notes:
- Extensions of the query methods, are provided as overloads of 'GetComponent' and 'GetComponents' for game objects and components. To use these extensions, remember to import the 'Utility' namespace first.
- The enumerators should be disposed of. Failure to do so, won't clear the results from the backing shared buffer, resulting in continuous growth of the buffer as well as unexpected results.
  - Using 'foreach' loops instead of explicit enumerators is recommended as they automatically take care of the disposal.
  - Using enumerators explicitly should be accompanied by a 'using' statement or its expanded 'try-finally' block to guarantee the disposal.
  - The enumerators should not be used after disposal. They won't throw any exceptions but will have invalid behaviour.
  - The enumerators should not be copied around as the disposal of one copy, affects all others by clearing the shared buffer.
- The methods are reentrant but inherently not thread-safe.
