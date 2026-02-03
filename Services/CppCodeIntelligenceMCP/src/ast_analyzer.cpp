#include "../include/code_intelligence.h"
#include <fstream>
#include <regex>
#include <sstream>

namespace CodeIntelligence {

std::vector<Symbol> ASTAnalyzer::analyzeFile(const std::string& filePath, 
                                            const CompileCommand& command) {
    std::vector<Symbol> symbols;
    
    // Read file content
    std::ifstream file(filePath);
    if (!file.is_open()) {
        return symbols;
    }
    
    std::string content((std::istreambuf_iterator<char>(file)),
                        std::istreambuf_iterator<char>());
    
    // Parse different types of symbols
    auto classes = parseClassDeclarations(content, filePath);
    auto functions = parseFunctionDeclarations(content, filePath);
    auto variables = parseVariableDeclarations(content, filePath);
    
    symbols.insert(symbols.end(), classes.begin(), classes.end());
    symbols.insert(symbols.end(), functions.begin(), functions.end());
    symbols.insert(symbols.end(), variables.begin(), variables.end());
    
    return symbols;
}

std::vector<Symbol> ASTAnalyzer::parseClassDeclarations(const std::string& content, 
                                                       const std::string& filePath) {
    std::vector<Symbol> symbols;
    
    // Simple regex for class declarations (basic implementation)
    std::regex classRegex(R"(class\s+(\w+))");
    std::smatch match;
    
    auto searchStart = content.cbegin();
    int lineNumber = 1;
    
    while (std::regex_search(searchStart, content.cend(), match, classRegex)) {
        Symbol symbol;
        symbol.name = match[1].str();
        symbol.type = "class";
        symbol.file = filePath;
        symbol.line = lineNumber; // Simplified line tracking
        symbol.column = 0;
        symbol.signature = "class " + symbol.name;
        
        symbols.push_back(symbol);
        searchStart = match.suffix().first;
    }
    
    return symbols;
}

std::vector<Symbol> ASTAnalyzer::parseFunctionDeclarations(const std::string& content, 
                                                          const std::string& filePath) {
    std::vector<Symbol> symbols;
    
    // Simple regex for function declarations
    std::regex funcRegex(R"((\w+)\s+(\w+)\s*\([^)]*\))");
    std::smatch match;
    
    auto searchStart = content.cbegin();
    int lineNumber = 1;
    
    while (std::regex_search(searchStart, content.cend(), match, funcRegex)) {
        Symbol symbol;
        symbol.name = match[2].str();
        symbol.type = "function";
        symbol.file = filePath;
        symbol.line = lineNumber; // Simplified line tracking
        symbol.column = 0;
        symbol.signature = match[0].str();
        
        symbols.push_back(symbol);
        searchStart = match.suffix().first;
    }
    
    return symbols;
}

std::vector<Symbol> ASTAnalyzer::parseVariableDeclarations(const std::string& content, 
                                                          const std::string& filePath) {
    std::vector<Symbol> symbols;
    
    // Basic variable parsing - this is a simplified implementation
    // In a real implementation, you'd want to use a proper C++ parser
    
    return symbols;
}

} // namespace CodeIntelligence
