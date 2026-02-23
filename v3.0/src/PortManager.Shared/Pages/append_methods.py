import re

# 读取 .razor 文件
with open('Ports.razor', 'r', encoding='utf-8') as f:
    razor_content = f.read()

# 读取现有的 .razor.cs 文件
with open('Ports.razor.cs', 'r', encoding='utf-8') as f:
    cs_content = f.read()

# 找到 @code 部分
code_start = razor_content.find('@code {')
if code_start != -1:
    # 提取 @code 部分
    code_section = razor_content[code_start:]
    
    # 移除 @code 关键字，保留大括号内的内容
    code_content = code_section[7:]  # 跳过 '@code {'
    
    # 找到最后一个闭合的大括号
    brace_count = 1
    end_index = 0
    for i, char in enumerate(code_content):
        if char == '{':
            brace_count += 1
        elif char == '}':
            brace_count -= 1
            if brace_count == 0:
                end_index = i
                break
    
    # 提取代码内容（不包括最后一个闭合大括号）
    code_body = code_content[:end_index]
    
    # 将代码追加到 .razor.cs 文件中（在 closing brace 之前）
    # 找到 .razor.cs 文件的最后一个闭合大括号
    last_brace = cs_content.rfind('}')
    if last_brace != -1:
        new_cs_content = cs_content[:last_brace] + '\n' + code_body + '\n' + cs_content[last_brace:]
    else:
        new_cs_content = cs_content + '\n' + code_body + '\n}'
    
    # 写入 .razor.cs 文件
    with open('Ports.razor.cs', 'w', encoding='utf-8') as f:
        f.write(new_cs_content)
    
    print(f'Methods appended successfully.')
    print(f'CS file size: {len(cs_content)} -> {len(new_cs_content)}')
else:
    print('@code section not found in .razor file')
