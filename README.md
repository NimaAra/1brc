1️⃣🐝🏎️ The One Billion Row Challenge - .NET Edition

---

This is the .NET implementation of the ([JAVA Challenge](https://github.com/gunnarmorling/1brc)). There has been a number of implementations already with the fastest record currently held by [Victor Baybekov](https://github.com/buybackoff) which you can find [HERE](https://github.com/buybackoff/1brc).

Given that the top leading solutions both in JAVA and .NET have so far had to rely on Unsafe code (using Pointers); my goal for this solution was to strictly avoid unsafe code to see how much optimization can be applied in managed code alone.

The result of this repo on my very old (and tired) workstation is ~800ms slower than Victor's solution.

| Repo       | Result (m:s.ms) | Implementation | Platform |
| ---------- | --------------- | -------------- | -------- |
| buybackoff | 00:04.603       | AOT            | Windows  |
| buybackoff | 00:04.821       | JIT            | Windows  |
| This       | 00:05.436       | AOT            | Windows  |
| This       | 00:05.625       | JIT            | Windows  |
