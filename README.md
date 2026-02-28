# Pathhack

needs dotnet9+

```bash
dotnet run
```

If it complains about bee, I think it's some strange dotnet version mismatch, do:

```bash
rm Bee.dll
(cd bee; dotnet build)


# this step should not be required...
# cp bee/bin/Debug/netstandard2.0/Bee.dll .

dotnet run
```

it will try to read ~/.pathhackrc - same format as all the other nethacks, in theory it should be able to read a .dnethackrc (symlink it) and will try to honour options that it understands, it shouldn't barf on unknown options.

