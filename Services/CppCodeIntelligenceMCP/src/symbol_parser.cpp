#include "../include/code_intelligence.h"
#include <algorithm>

namespace CodeIntelligence {

void SymbolDatabase::addSymbol(const Symbol& symbol) {
    symbolsByName[symbol.name].push_back(symbol);
    symbolsByFile[symbol.file].push_back(symbol);
    symbolsByType[symbol.type].push_back(symbol);
}

std::vector<Symbol> SymbolDatabase::findSymbol(const std::string& name) const {
    auto it = symbolsByName.find(name);
    if (it != symbolsByName.end()) {
        return it->second;
    }
    return {};
}

std::vector<Symbol> SymbolDatabase::findSymbolsInFile(const std::string& file) const {
    auto it = symbolsByFile.find(file);
    if (it != symbolsByFile.end()) {
        return it->second;
    }
    return {};
}

std::vector<Symbol> SymbolDatabase::findSymbolsByType(const std::string& type) const {
    auto it = symbolsByType.find(type);
    if (it != symbolsByType.end()) {
        return it->second;
    }
    return {};
}

} // namespace CodeIntelligence
