# ComponentQuery
Sophisticated garbage-free component iteration and retrieval for Unity game objects.

Note:
- Overloaded extensions of GetComponent and GetComponents are provided for game objects. To use these extensions, remember to import the namespace first.
- The enumerators must be disposed of. Failure to do so, won't clear the results from the backing shared buffer, resulting in continuous growth of the buffer and invalid behaviours.
  - Using foreach loops instead of explicit enumerators is recommended as they automatically take care of the disposal.
  - Using enumerators explicitly should be accompanied by 'using' statement or its expanded 'try-finally' block to guarantee the disposal.
  - The enumerators should not be used after disposal. They won't throw any exception and will have invalid behavior.
  - The enumerators must not be copied around as the disposal of one enumerator, affects all other copies by clearing the shared buffer.
- The methods are reentrant but they are not threadsafe.
