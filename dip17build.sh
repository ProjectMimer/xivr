# I don't want to talk about this

set -e

cd xivr_main

mkdir /scratch
mkdir /scratch/openvr_local
7za x -y -o/scratch/openvr_local -tzip /needs/openvr.zip

OPENVR_VERSION="1.23.7"
OPENVR_LOCAL="/scratch/openvr_local/openvr-$OPENVR_VERSION"
LINKER_OBJS=""

compile () {
	/opt/msvc/bin/x64/cl /permissive- /ifcOutput "x64\Release\\" /GS /GL /W3 /Gy /Zc:wchar_t /Zi /Gm- /O2 /sdl /Fd"x64\Release\vc143.pdb" /Zc:inline \
	/fp:precise /D "NDEBUG" /D "XIVRMAIN_EXPORTS" /D "_WINDOWS" /D "_USRDLL" /D "_WINDLL" /D "_UNICODE" /D "UNICODE" /errorReport:prompt /WX- /Zc:forScope \
	/Gd /Oi /MD /FC /Fa"x64\Release\\" /EHsc /nologo /Fo"x64\Release\\" /Fp"x64\Release\xivr_main.pch" /diagnostics:column /I"$OPENVR_LOCAL/headers" /c $1
	LINKER_OBJS="$LINKER_OBJS x64\Release\\${1/cpp/obj}"
}

for FILE in *.cpp; do compile $FILE; done

echo $LINKER_OBJS

# /PGD:"x64\Release\xivr_main.pgd" causes errors and I don't know what it does
/opt/msvc/bin/x64/link /OUT:"x64\Release\xivr_main.dll" /MANIFEST /LTCG:incremental /NXCOMPAT /PDB:"x64\Release\xivr_main.pdb" \
	/DYNAMICBASE "d3dcompiler.lib" "d3d11.lib" "dxgi.lib" "openvr_api.lib" "kernel32.lib" "user32.lib" "gdi32.lib" "winspool.lib" "comdlg32.lib" \
	"advapi32.lib" "shell32.lib" "ole32.lib" "oleaut32.lib" "uuid.lib" "odbc32.lib" "odbccp32.lib" /IMPLIB:"x64\Release\xivr_main.lib" /DEBUG \
	/DLL /MACHINE:X64 /OPT:REF /SUBSYSTEM:WINDOWS /MANIFESTUAC:NO /ManifestFile:"x64\Release\xivr_main.dll.intermediate.manifest" \
	/LTCGOUT:"x64\Release\xivr_main.iobj" /OPT:ICF /ERRORREPORT:PROMPT /ILK:"x64\Release\xivr_main.ilk" /NOLOGO /LIBPATH:"$OPENVR_LOCAL/lib/win64" \
	/TLBID:1 $LINKER_OBJS

cp x64/Release/xivr_main.dll ../xivr
cp x64/Release/xivr_main.pdb ../xivr
cp $OPENVR_LOCAL/bin/win64/openvr_api.dll ../xivr
