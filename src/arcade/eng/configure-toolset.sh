function Test-FilesUseTelemetryOutput {
    _files_use_telemetry_result=0
    declare -a require_telemetry_exclude_files
    require_telemetry_exclude_files=(
        'eng/common/build.sh'
        'eng/common/cibuild.sh'
        'eng/common/native/init-compiler.sh'
        'eng/common/native/init-distro-rid.sh'
        'eng/common/native/init-os-and-arch.sh'
        'eng/common/native/install-dependencies.sh'
        'eng/common/cross/tizen-build-rootfs.sh'
        'eng/common/cross/tizen-fetch.sh'
        'eng/common/cross/build-android-rootfs.sh'
        'eng/common/cross/build-rootfs.sh'
        'eng/common/darc-init.sh'
        'eng/common/dotnet.sh'
        'eng/common/msbuild.sh'
        'eng/common/performance/performance-setup.sh'
        'eng/common/vmr-sync.sh'
    )

    local file_list=`grep --files-without-match --recursive --include=*.sh "Write-PipelineTelemetryError" $scriptroot`
    for file in $file_list; do
        for remove_file in ${require_telemetry_exclude_files[@]}; do
            if [[ $file =~ .*"$remove_file" ]]; then
            file_list=( "${file_list[@]/$file}" )
            fi
        done
    done

    if [[ -n "${file_list//[[:space:]]/}" ]]; then
        Write-PipelineTelemetryError -force -category 'Build' 'One or more eng/common scripts do not use telemetry categorization.'
        echo "https://github.com/dotnet/arcade/blob/master/Documentation/Projects/DevOps/CI/Telemetry-Guidance.md"
        echo "The following bash files do not include telemetry categorization output:"
        for file in $file_list
            do echo "'$file'"
        done

        _files_use_telemetry_result=1
        return
    fi
}

ResolvePath "${BASH_SOURCE[0]}"
scriptroot=`dirname "$_ResolvePath"`

Test-FilesUseTelemetryOutput

if [[ $_files_use_telemetry_result != 0 ]]; then
  exit $_files_use_telemetry_result
fi
